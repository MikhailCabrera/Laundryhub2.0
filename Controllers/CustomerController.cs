using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Data;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Controllers;

[Authorize(Roles = "Customer,Admin")]
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
    public async Task<IActionResult> Index(string? tab = null)
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
            if (!order.IsPaymentConfirmed && order.TotalAmount.HasValue && order.ReadyForDeliveryNotifiedAt.HasValue && !order.IsAbandoned)
            {
                var deadline = order.ReadyForDeliveryNotifiedAt.Value.AddHours(24);
                if (now > deadline)
                {
                    var overdueSpan = now - deadline;
                    var daysOverdue = (decimal)overdueSpan.TotalDays;

                    // Penalty: 3% of TotalAmount per day overdue, capped at 30%
                    var rawPenalty = daysOverdue * 0.03m * order.TotalAmount.Value;
                    var maxPenalty = order.TotalAmount.Value * 0.30m;
                    order.AccruedPenaltyAmount = Math.Round(Math.Min(rawPenalty, maxPenalty), 2);

                    // Abandonment at 15 days overdue
                    if (daysOverdue >= 15m)
                    {
                        order.IsAbandoned = true;
                        order.AbandonedAt = now;
                        order.Status = OrderStatus.Abandoned;
                        order.UpdatedAt = now;
                    }
                    ordersModified = true;
                }
                else
                {
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
        foreach (var order in orders.Where(o => !o.IsPaymentConfirmed && o.TotalAmount.HasValue))
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

        ViewData["ActiveTab"] = tab ?? "dashboard";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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

        if (!input.AcceptTerms)
        {
            TempData["CustomerError"] = "You must agree to the Terms & Conditions to book.";
            return RedirectToAction("Index", new { tab = "book" });
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
        var cancelUrl = $"{baseUrl}/Customer?tab=payment";

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
            return RedirectToAction("Index", new { tab = "tracking" });

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

        LaundryHub2._0.Services.OrderPaymentConfirmation.ApplyConfirmedPayment(order, DateTime.UtcNow);
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
                return RedirectToAction("Index", new { tab = "tracking" });
            }
            TempData["CustomerError"] = "Another update conflicted with this payment. Please check your order status and try again.";
            return RedirectToAction("Index", new { tab = "payment" });
        }
        TempData["CustomerSuccess"] = $"🎉 Payment verified successfully for order #{order.OrderNumber}! Your laundry is ready for delivery dispatch.";

        return RedirectToAction("Index", new { tab = "tracking" });
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

        order.WeightConfirmedByCustomerAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        LaundryHub2._0.Services.AuditTrail.Record(_context, user.FullName, LaundryHub2._0.Services.AuditTrail.Update, user.FullName, "Customer", $"Order {order.OrderNumber} weight confirmed by customer ({order.WeightKg} kg).");
        await _notificationService.NotifyAsync(order.CustomerId, order.Id, LaundryHub2._0.Services.NotificationService.WeightConfirmed, $"Order {order.OrderNumber} weighed", $"You confirmed the verified weight of {order.WeightKg} kg.");
        await _context.SaveChangesAsync();

        TempData["CustomerSuccess"] = $"Weight confirmed for order {order.OrderNumber}. Washing can now begin.";
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
}
