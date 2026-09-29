using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Data;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Controllers;

[Authorize(Roles = "Customer")]
public class CustomerController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly LaundryHub2._0.Services.PayMongoService _payMongoService;
    private readonly ILogger<CustomerController> _logger;
    private readonly LaundryHub2._0.Services.NotificationService _notificationService;
    private readonly LaundryHub2._0.Services.OrderNumberService _orderNumberService;
    private readonly LaundryHub2._0.Services.LoyaltyService _loyaltyService;

    public CustomerController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        LaundryHub2._0.Services.PayMongoService payMongoService,
        ILogger<CustomerController> logger,
        LaundryHub2._0.Services.NotificationService notificationService,
        LaundryHub2._0.Services.OrderNumberService orderNumberService,
        LaundryHub2._0.Services.LoyaltyService loyaltyService)
    {
        _context = context;
        _userManager = userManager;
        _payMongoService = payMongoService;
        _logger = logger;
        _notificationService = notificationService;
        _orderNumberService = orderNumberService;
        _loyaltyService = loyaltyService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? tab = null, [FromQuery(Name = "order")] int? trackingOrder = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var services = await _context.LaundryServices
            .Where(s => s.IsActive)
            .OrderBy(s => s.Id)
            .ToListAsync();

        var orders = await _context.LaundryOrders
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .Include(o => o.PickupRider)
            .Include(o => o.DeliveryRider)
            .Where(o => o.CustomerId == user.Id)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var ordersModified = false;

        foreach (var order in orders)
        {
            // Only process unpaid, non-abandoned orders that have a stored payment deadline.
            if (!order.IsPaymentConfirmed && order.TotalAmount.HasValue && order.PaymentDeadlineAt.HasValue && !order.IsAbandoned)
            {
                var deadline = order.PaymentDeadlineAt.Value;
                var gracePeriodEnd = order.GracePeriodEndAt ?? deadline.AddHours(72);

                if (now > deadline)
                {
                    // Abandonment: exactly 15 days after the ORIGINAL payment deadline.
                    if (now >= deadline.AddDays(15))
                    {
                        order.IsAbandoned = true;
                        order.AbandonedAt = now;
                        order.Status = OrderStatus.Abandoned;
                        order.UpdatedAt = now;
                        order.AccruedPenaltyAmount = Math.Round(order.TotalAmount.Value * 0.30m, 2); // cap
                        ordersModified = true;
                    }
                    else if (now > gracePeriodEnd)
                    {
                        // Penalty: 3% of TotalAmount per day overdue (measured from GracePeriodEndAt),
                        // capped at 30% (10 days).
                        var overdueSpan = now - gracePeriodEnd;
                        var daysOverdue = (decimal)overdueSpan.TotalDays;
                        var rawPenalty = daysOverdue * 0.03m * order.TotalAmount.Value;
                        var maxPenalty = order.TotalAmount.Value * 0.30m;
                        order.AccruedPenaltyAmount = Math.Round(Math.Min(rawPenalty, maxPenalty), 2);
                        ordersModified = true;
                    }
                    else
                    {
                        // Inside 72-hour grace period: no penalty yet.
                        order.AccruedPenaltyAmount = 0m;
                        ordersModified = true;
                    }
                }
                else
                {
                    // Before deadline: no penalty.
                    order.AccruedPenaltyAmount = 0m;
                }
            }
        }

        if (ordersModified)
        {
            await _context.SaveChangesAsync();
        }

        var notifications = await _context.Notifications
            .Where(n => n.RecipientUserId == user.Id)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

        var loyaltyBalance = await _loyaltyService.GetBalanceAsync(user.Id);
        var loyaltyLedger = await _context.LoyaltyTransactions
            .Where(t => t.CustomerId == user.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(30)
            .ToListAsync();
        var loyaltyDiscounts = new Dictionary<int, decimal>();
        foreach (var order in orders.Where(o => o.TotalAmount.HasValue))
            loyaltyDiscounts[order.Id] = await _loyaltyService.GetRedeemedDiscountAsync(order.Id, order.TotalAmount ?? 0m);

        var model = new CustomerDashboardViewModel
        {
            User = user,
            AvailableServices = services,
            Orders = orders,
            Notifications = notifications,
            LoyaltyBalance = loyaltyBalance,
            LoyaltyLedger = loyaltyLedger,
            LoyaltyDiscounts = loyaltyDiscounts
        };

        // Deep link: ?tab=tracking&order=<id> shows exactly that order when it belongs
        // to the caller; anything else falls back to the default selection in the view.
        int? trackingOrderId = null;
        if (trackingOrder.HasValue && orders.Any(o => o.Id == trackingOrder.Value))
            trackingOrderId = trackingOrder.Value;

        ViewData["ActiveTab"] = tab ?? "dashboard";
        ViewData["TrackingOrderId"] = trackingOrderId;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("action-policy")]
    public async Task<IActionResult> BookPickup(BookPickupInputModel input)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (input.SelectedServiceIds == null || input.SelectedServiceIds.Count == 0)
        {
            TempData["CustomerError"] = "Please select at least one laundry service.";
            return RedirectToAction("Index", new { tab = "book" });
        }

        if (string.IsNullOrWhiteSpace(input.PickupLocation))
        {
            TempData["CustomerError"] = "Please specify a pickup location or address.";
            return RedirectToAction("Index", new { tab = "book" });
        }

        // Ops run Monday–Saturday; the shop is closed on Sundays.
        if (input.PreferredPickupDate.DayOfWeek == DayOfWeek.Sunday)
        {
            TempData["CustomerError"] = "Pickups are scheduled Monday to Saturday only — the shop is closed on Sundays. Please choose another date.";
            return RedirectToAction("Index", new { tab = "book" });
        }

        if (!input.AcceptTerms)
        {
            TempData["CustomerError"] = "You must agree to the Terms & Conditions to book.";
            return RedirectToAction("Index", new { tab = "book" });
        }

        // Validate optional exact map coordinates if provided by customer
        if (input.PickupLatitude.HasValue || input.PickupLongitude.HasValue)
        {
            if (!input.PickupLatitude.HasValue || !input.PickupLongitude.HasValue ||
                input.PickupLatitude.Value < -90m || input.PickupLatitude.Value > 90m ||
                input.PickupLongitude.Value < -180m || input.PickupLongitude.Value > 180m)
            {
                TempData["CustomerError"] = "Invalid pickup map coordinates. Please select a valid point on the map.";
                return RedirectToAction("Index", new { tab = "book" });
            }
        }

        // Clean & format mobile number
        var contact = input.ContactNumber.Trim().Replace(" ", "").Replace("-", "");
        if (contact.StartsWith("09"))
        {
            contact = "+63" + contact.Substring(1);
        }

        LaundryOrder order;
        try
        {
            order = await _orderNumberService.CreateOrderWithUniqueNumberAsync(orderNumber => new LaundryOrder
            {
                OrderNumber = orderNumber,
                CustomerId = user.Id,
                PreferredPickupDate = input.PreferredPickupDate,
                PreferredPickupTime = input.PreferredPickupTime,
                PickupLocation = input.PickupLocation.Trim(),
                PickupLatitude = input.PickupLatitude,
                PickupLongitude = input.PickupLongitude,
                ContactNumber = contact,
                SpecialInstructions = input.SpecialInstructions?.Trim(),
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                TermsAcceptedAt = DateTime.UtcNow,
                TermsVersion = LaundryOrder.CurrentTermsVersion
            });
        }
        catch (LaundryHub2._0.Services.DuplicateOrderNumberException)
        {
            TempData["CustomerError"] = "We could not reserve a booking reference after 3 attempts. Please try again — no order was created.";
            return RedirectToAction("Index", new { tab = "book" });
        }
        catch (Exception ex)
        {
            TempData["CustomerError"] = $"Unable to complete booking: {ex.Message}";
            return RedirectToAction("Index", new { tab = "book" });
        }

        try
        {
            // Attach snapshot of selected services
            var services = await _context.LaundryServices
                .Where(s => input.SelectedServiceIds.Contains(s.Id))
                .ToListAsync();

            foreach (var svc in services)
            {
                _context.LaundryOrderServices.Add(new LaundryOrderService
                {
                    OrderId = order.Id,
                    ServiceId = svc.Id,
                    PricePerKgSnapshot = svc.PricePerKg
                });
            }

            await _context.SaveChangesAsync();

            TempData["CustomerSuccess"] = $"Booking {order.OrderNumber} submitted successfully! Your request is pending rider assignment.";
            return RedirectToAction("Index", new { tab = "history" });
        }
        catch (Exception ex)
        {
            TempData["CustomerError"] = $"Unable to complete booking: {ex.Message}";
            return RedirectToAction("Index", new { tab = "book" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var order = await _context.LaundryOrders
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == user.Id);

        if (order == null)
        {
            TempData["CustomerError"] = "Order not found.";
            return RedirectToAction("Index", new { tab = "history" });
        }

        var isPending = order.Status == OrderStatus.Pending;
        var isCancelableWalkIn = order.Origin == OrderOrigin.WalkIn
            && order.Status == OrderStatus.Weighing
            && order.WeightKg == null;
        if ((!isPending && !isCancelableWalkIn) || !string.IsNullOrEmpty(order.PickupRiderId))
        {
            TempData["CustomerError"] = "Orders can only be cancelled while pending and before a rider is assigned.";
            return RedirectToAction("Index", new { tab = "history" });
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        LaundryHub2._0.Services.AuditTrail.Record(_context, user.FullName, LaundryHub2._0.Services.AuditTrail.Update, user.FullName, "Customer", $"Order {order.OrderNumber} cancelled by customer.");
        await _context.SaveChangesAsync();

        TempData["CustomerSuccess"] = $"Order {order.OrderNumber} was successfully cancelled.";
        return RedirectToAction("Index", new { tab = "history" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("action-policy")]
    public async Task<IActionResult> PayOrder(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var order = await _context.LaundryOrders
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == user.Id);

        if (order == null)
        {
            TempData["CustomerError"] = "Order not found.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        if (order.IsPaymentConfirmed)
        {
            TempData["CustomerError"] = "This order is already paid.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        var amountToPay = await _loyaltyService.ComputeAmountDueAsync(order);
        if (amountToPay <= 0)
        {
            TempData["CustomerError"] = "Order amount is invalid or awaiting weighing.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var successUrl = $"{baseUrl}/Customer/PaymentSuccess?orderId={order.Id}";
        var cancelUrl = $"{baseUrl}/Customer/PaymentCancelled?orderId={order.Id}";

        var checkout = await _payMongoService.CreateCheckoutSessionAsync(
            order.Id,
            order.OrderNumber,
            amountToPay,
            $"Laundry Hub Payment for #{order.OrderNumber}",
            successUrl,
            cancelUrl);

        if (checkout != null && !string.IsNullOrEmpty(checkout.CheckoutUrl))
        {
            order.PayMongoCheckoutUrl = checkout.CheckoutUrl;
            order.PayMongoPaymentId = checkout.CheckoutSessionId;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Redirect(checkout.CheckoutUrl);
        }

        // No silent simulation: if no checkout session was created, the order
        // stays unpaid. Marking it paid here would dispatch laundry that was
        // never paid for.
        _logger.LogWarning("PayMongo checkout creation failed for order {OrderNumber} (customer {CustomerId}); order left unpaid.",
            order.OrderNumber, user.Id);
        TempData["CustomerError"] = "Online payment is currently unavailable. Please try again later — your order was not charged and is not marked as paid.";
        return RedirectToAction("Index", new { tab = "payment" });
    }

    [HttpGet]
    public async Task<IActionResult> PaymentSuccess(int orderId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var order = await _context.LaundryOrders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == user.Id);

        if (order == null)
        {
            TempData["CustomerError"] = "Order not found.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        if (order.IsPaymentConfirmed)
            return RedirectToAction(nameof(Receipt), new { id = order.Id });

        // Never trust the redirect URL alone: confirm with PayMongo that the
        // recorded checkout session was actually paid (and covers the amount
        // due) before marking anything paid.
        var amountDue = await _loyaltyService.ComputeAmountDueAsync(order);
        var verification = await _payMongoService.VerifySessionPaidAsync(
            order.PayMongoPaymentId ?? string.Empty, amountDue);

        if (!verification.Paid)
        {
            _logger.LogWarning("Payment verification failed for order {OrderNumber}: {Reason}",
                order.OrderNumber, verification.Reason);
            TempData["CustomerError"] = verification.Reason;
            return RedirectToAction("Index", new { tab = "payment" });
        }

        var applied = LaundryHub2._0.Services.OrderPaymentConfirmation.ApplyConfirmedPayment(order, DateTime.UtcNow);
        if (!applied)
        {
            _logger.LogWarning("ApplyConfirmedPayment declined for order {OrderNumber} (status={Status}, isConfirmed={IsConfirmed})",
                order.OrderNumber, order.Status, order.IsPaymentConfirmed);
            TempData["CustomerError"] = "Payment could not be applied to this order at this time.";
            return RedirectToAction("Index", new { tab = "payment" });
        }
        LaundryHub2._0.Services.AuditTrail.Record(_context, user.FullName, LaundryHub2._0.Services.AuditTrail.Payment, user.FullName, "Customer", $"Order {order.OrderNumber} paid {amountDue:N2} via redirect verify.");
        if (order.Status == OrderStatus.ReadyForDelivery)
            await _notificationService.NotifyAsync(order.CustomerId, order.Id, LaundryHub2._0.Services.NotificationService.ReadyForDelivery, $"Order {order.OrderNumber} ready for delivery", "Your laundry is ready and will be dispatched for delivery.");
        else
            await _notificationService.NotifyAsync(order.CustomerId, order.Id, LaundryHub2._0.Services.NotificationService.PaymentConfirmed, $"Payment confirmed for order {order.OrderNumber}", $"Payment of ₱{amountDue:N2} received. Thank you!");

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            var fresh = await _context.LaundryOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == order.Id);
            if (fresh != null && fresh.IsPaymentConfirmed)
            {
                TempData["CustomerSuccess"] = $"🎉 Payment verified successfully for order #{order.OrderNumber}! Your laundry is ready for delivery dispatch.";
                return RedirectToAction(nameof(Receipt), new { id = order.Id });
            }
            TempData["CustomerError"] = "Another update conflicted with this payment. Please check your order status and try again.";
            return RedirectToAction("Index", new { tab = "payment" });
        }
        TempData["CustomerSuccess"] = $"🎉 Payment verified successfully for order #{order.OrderNumber}! Your laundry is ready for delivery dispatch.";

        return RedirectToAction(nameof(Receipt), new { id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> PaymentCancelled(int orderId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var order = await _context.LaundryOrders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == user.Id);

        if (order == null)
        {
            TempData["CustomerError"] = "Order not found.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        // Paid in the meantime (e.g. webhook won the race): show the receipt.
        if (order.IsPaymentConfirmed)
            return RedirectToAction(nameof(Receipt), new { id = order.Id });

        // Cancel path: nothing is marked paid here. The order stays exactly as
        // it was (AwaitingPayment) and the customer can retry payment.
        TempData["CustomerError"] = $"Payment for order {order.OrderNumber} was cancelled — no charge was made and the order is not marked as paid. You can retry payment anytime.";
        return RedirectToAction("Index", new { tab = "payment" });
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var order = await _context.LaundryOrders
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == user.Id);

        if (order == null)
        {
            TempData["CustomerError"] = "Order not found.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        // Receipts exist only for verified payments — never for unpaid orders.
        if (!order.IsPaymentConfirmed)
        {
            TempData["CustomerError"] = $"Order {order.OrderNumber} is not paid yet, so no receipt is available.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        var baseAmount = order.TotalAmount ?? 0m;
        var penalty = order.AccruedPenaltyAmount ?? 0m;
        var discount = await _loyaltyService.GetRedeemedDiscountAsync(order.Id, baseAmount);

        return View(new PaymentReceiptViewModel
        {
            Order = order,
            CustomerName = user.FullName,
            BaseAmount = baseAmount,
            PenaltyAmount = penalty,
            LoyaltyDiscount = discount,
            NetPaid = Math.Max(0m, baseAmount + penalty - discount)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationsRead(int? id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var query = _context.Notifications.Where(n => n.RecipientUserId == user.Id && !n.IsRead);
        if (id.HasValue)
            query = query.Where(n => n.Id == id.Value);
        await query.ExecuteUpdateAsync(n => n.SetProperty(x => x.IsRead, true));

        return RedirectToAction("Index", new { tab = "dashboard" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmWeight(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var order = await _context.LaundryOrders
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == user.Id);

        if (order == null)
        {
            TempData["CustomerError"] = "Order not found.";
            return RedirectToAction("Index", new { tab = "tracking" });
        }

        if (order.WeightConfirmedByCustomerAt.HasValue)
        {
            TempData["CustomerSuccess"] = $"Weight already confirmed for order {order.OrderNumber}.";
            return RedirectToAction("Index", new { tab = "tracking" });
        }

        if (order.Status != OrderStatus.WeightConfirmed)
        {
            TempData["CustomerError"] = "This order cannot be confirmed at this time.";
            return RedirectToAction("Index", new { tab = "tracking" });
        }

        var nowWC = DateTime.UtcNow;
        order.WeightConfirmedByCustomerAt = nowWC;
        order.Status = OrderStatus.AwaitingPayment;
        order.UpdatedAt = nowWC;

        // Set immutable payment timeline anchors (only on first entry into AwaitingPayment).
        if (!order.AwaitingPaymentAt.HasValue)
        {
            order.AwaitingPaymentAt = nowWC;
            order.PaymentDeadlineAt = nowWC.AddHours(24);
            order.GracePeriodEndAt = nowWC.AddHours(24 + 72); // 24h deadline + 72h grace
        }

        LaundryHub2._0.Services.AuditTrail.Record(_context, user.FullName, LaundryHub2._0.Services.AuditTrail.Update, user.FullName, "Customer", $"Order {order.OrderNumber} weight confirmed by customer ({order.WeightKg} kg).");
        await _notificationService.NotifyAsync(order.CustomerId, order.Id, LaundryHub2._0.Services.NotificationService.WeightConfirmed, $"Order {order.OrderNumber} weighed", $"You confirmed the verified weight of {order.WeightKg} kg.");
        await _context.SaveChangesAsync();

        TempData["CustomerSuccess"] = $"Weight confirmed for order {order.OrderNumber}. Payment can now be completed.";
        return RedirectToAction("Index", new { tab = "tracking" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RedeemPoints(RedeemPointsInputModel input)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            TempData["CustomerError"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        var order = await _context.LaundryOrders
            .FirstOrDefaultAsync(o => o.Id == input.OrderId && o.CustomerId == user.Id);
        if (order == null)
        {
            TempData["CustomerError"] = "Order not found.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        if (!order.WeightConfirmedAt.HasValue || !order.TotalAmount.HasValue || order.TotalAmount <= 0 || order.IsPaymentConfirmed)
        {
            TempData["CustomerError"] = "Points can only be redeemed on unpaid, weight-confirmed orders.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        if (input.Points < 100 || input.Points % 100 != 0)
        {
            TempData["CustomerError"] = "Points must be at least 100 and in multiples of 100.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        var balance = await _loyaltyService.GetBalanceAsync(user.Id);
        if (input.Points > balance)
        {
            TempData["CustomerError"] = $"Insufficient loyalty balance. Available: {balance} points.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        if (await _context.LoyaltyTransactions.AnyAsync(t => t.OrderId == order.Id && t.Type == LaundryHub2._0.Models.LoyaltyTransactionType.Redeem))
        {
            TempData["CustomerError"] = "Points have already been redeemed for this order.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        _context.LoyaltyTransactions.Add(new LaundryHub2._0.Models.LoyaltyTransaction
        {
            CustomerId = user.Id,
            OrderId = order.Id,
            Type = LaundryHub2._0.Models.LoyaltyTransactionType.Redeem,
            Points = -input.Points,
            Reason = $"Redeemed {input.Points} points on order {order.OrderNumber}",
            CreatedAt = DateTime.UtcNow
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            if (await _context.LoyaltyTransactions.AnyAsync(t => t.OrderId == order.Id && t.Type == LaundryHub2._0.Models.LoyaltyTransactionType.Redeem))
            {
                TempData["CustomerError"] = "Points have already been redeemed for this order.";
                return RedirectToAction("Index", new { tab = "payment" });
            }
            TempData["CustomerError"] = "Could not record the redemption. Please try again.";
            return RedirectToAction("Index", new { tab = "payment" });
        }

        var discount = Math.Min(input.Points / 100m * 10m, order.TotalAmount.Value);
        TempData["CustomerSuccess"] = $"Redeemed {input.Points} points (₱{discount:N2} off order {order.OrderNumber}).";
        return RedirectToAction("Index", new { tab = "payment" });
    }

    // POST /Customer/FileClaim — customer self-report of damage/loss on one of
    // their own DELIVERED orders. Owner-scoped, antiforgery, JSON.
    // Abuse controls (all server-enforced, nothing persisted on rejection):
    // the order must belong to the caller, must be Delivered, and each
    // customer is capped at 3 open claims (explicit rejection beyond).
    // Staff resolve path (Admin/ResolveClaim) is untouched.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FileClaim(int orderId, string? type, string? description)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Json(new { success = false, message = "Please log in again." });

        if (type != LaundryHub2._0.Models.ClaimType.Damage && type != LaundryHub2._0.Models.ClaimType.Loss && type != LaundryHub2._0.Models.ClaimType.Other)
            return Json(new { success = false, message = "Claim type must be Damage, Loss, or Other." });

        var text = (description ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return Json(new { success = false, message = "Description is required." });
        if (text.Length > 1000)
            return Json(new { success = false, message = "Description cannot exceed 1000 characters." });

        var order = await _context.LaundryOrders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == user.Id);
        if (order == null)
            return Json(new { success = false, message = "Order not found." });

        if (order.Status != LaundryHub2._0.Models.OrderStatus.Delivered)
            return Json(new { success = false, message = "Only delivered orders can be disputed." });

        var openCount = await _context.Claims
            .Where(c => c.Status == LaundryHub2._0.Models.ClaimStatus.Open && c.Order != null && c.Order.CustomerId == user.Id)
            .CountAsync();
        if (openCount >= 3)
            return Json(new { success = false, message = "You already have 3 open claims. Please wait until one is resolved before filing another." });

        _context.Claims.Add(new LaundryHub2._0.Models.Claim
        {
            OrderId = order.Id,
            ReporterName = user.FullName,
            ReporterIsStaff = false,
            Type = type,
            Description = text,
            Status = LaundryHub2._0.Models.ClaimStatus.Open,
            CreatedAt = DateTime.UtcNow
        });
        LaundryHub2._0.Services.AuditTrail.Record(_context, user.FullName, LaundryHub2._0.Services.AuditTrail.Claim, order.OrderNumber, "Order", $"{type} claim self-filed by customer on order {order.OrderNumber}.");
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"{type} claim filed on order {order.OrderNumber}. Our team will review it shortly." });
    }

    /// <summary>
    /// Phase 2E: Secure endpoint for customers to retrieve the latest location of their assigned active rider.
    /// Strictly verifies customer ownership and active tracking window.
    /// Returns only minimal location fields (latitude, longitude, updatedAt, isStale, trackingType).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRiderLocation(int orderId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Unauthorized(new { success = false, message = "Authentication required." });

        var order = await _context.LaundryOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == user.Id);

        if (order == null)
            return NotFound(new { success = false, message = "Order not found or access denied." });

        // Determine tracking eligibility and which rider is active
        // Pickup tracking: RiderAssigned, PickedUp, InTransitToShop (ends at Weighing)
        // Delivery tracking: ReadyForDelivery, OutForDelivery, DeliveryAttemptFailed (ends at Delivered)
        string? activeRiderId = null;
        string trackingType = "none";

        if (order.Status == OrderStatus.RiderAssigned ||
            order.Status == OrderStatus.PickedUp ||
            order.Status == OrderStatus.InTransitToShop)
        {
            trackingType = "pickup";
            activeRiderId = order.PickupRiderId;
        }
        else if (order.Status == OrderStatus.ReadyForDelivery ||
                 order.Status == OrderStatus.OutForDelivery ||
                 order.Status == OrderStatus.DeliveryAttemptFailed)
        {
            trackingType = "delivery";
            activeRiderId = order.DeliveryRiderId;
        }

        if (string.IsNullOrEmpty(activeRiderId) || trackingType == "none")
        {
            return Ok(new
            {
                success = false,
                isTrackingActive = false,
                trackingType = "none",
                message = "Live tracking is not active for this order."
            });
        }

        var rider = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == activeRiderId)
            .Select(u => new
            {
                u.CurrentLatitude,
                u.CurrentLongitude,
                u.LastLocationUpdatedAt
            })
            .FirstOrDefaultAsync();

        if (rider == null || !rider.CurrentLatitude.HasValue || !rider.CurrentLongitude.HasValue)
        {
            return Ok(new
            {
                success = true,
                isTrackingActive = true,
                trackingType,
                hasLocation = false,
                message = "Waiting for rider location..."
            });
        }

        var isStale = rider.LastLocationUpdatedAt == null || (DateTime.UtcNow - rider.LastLocationUpdatedAt.Value).TotalMinutes > 2;

        return Ok(new
        {
            success = true,
            isTrackingActive = true,
            trackingType,
            hasLocation = true,
            latitude = rider.CurrentLatitude.Value,
            longitude = rider.CurrentLongitude.Value,
            updatedAt = rider.LastLocationUpdatedAt.HasValue
                ? DateTime.SpecifyKind(rider.LastLocationUpdatedAt.Value, DateTimeKind.Utc).ToString("o")
                : null,
            isStale
        });
    }
}

