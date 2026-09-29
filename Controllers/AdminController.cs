using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Data;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Controllers;

[Authorize(Roles = "Admin,Manager,Staff")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly LaundryHub2._0.Services.NotificationService _notificationService;
    private readonly LaundryHub2._0.Services.OrderNumberService _orderNumberService;
    private readonly LaundryHub2._0.Services.LoyaltyService _loyaltyService;
    private readonly LaundryHub2._0.Services.PayMongoService _payMongoService;
    private readonly LaundryHub2._0.Services.PaymentDeadlineBackfillService _backfillService;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env, LaundryHub2._0.Services.NotificationService notificationService, LaundryHub2._0.Services.OrderNumberService orderNumberService, LaundryHub2._0.Services.LoyaltyService loyaltyService, LaundryHub2._0.Services.PayMongoService payMongoService, LaundryHub2._0.Services.PaymentDeadlineBackfillService backfillService)
    {
        _context = context;
        _userManager = userManager;
        _env = env;
        _notificationService = notificationService;
        _orderNumberService = orderNumberService;
        _loyaltyService = loyaltyService;
        _payMongoService = payMongoService;
        _backfillService = backfillService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? tab = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);

        var orders = await _context.LaundryOrders
            .Include(o => o.Customer)
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .Include(o => o.PickupRider)
            .Include(o => o.DeliveryRider)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var allRiders = await _userManager.GetUsersInRoleAsync("Rider");
        var activeRiders = allRiders
            .Where(r => !r.IsSuspended && !r.IsArchived)
            .OrderBy(r => r.FullName)
            .ToList();

        var allCustomers = await _userManager.GetUsersInRoleAsync("Customer");
        var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
        var managerUsers = await _userManager.GetUsersInRoleAsync("Manager");
        var staffUsers = await _userManager.GetUsersInRoleAsync("Staff");
        var riderCount = allRiders.Count;

        var employees = new List<AdminUserRowViewModel>();
        foreach (var (role, users) in new (string, IList<ApplicationUser>)[]
                 {
                     ("Admin", adminUsers),
                     ("Manager", managerUsers),
                     ("Staff", staffUsers),
                     ("Rider", allRiders),
                 })
        {
            foreach (var u in users)
            {
                employees.Add(new AdminUserRowViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? string.Empty,
                    PhoneNumber = u.PhoneNumber,
                    District = u.District,
                    CreatedAt = u.CreatedAt,
                    IsSuspended = u.IsSuspended,
                    SuspendNote = u.SuspendNote,
                    IsArchived = u.IsArchived,
                    Role = role
                });
            }
        }
        employees = employees.OrderByDescending(e => e.CreatedAt).ToList();

        var customers = allCustomers
            .Select(u => new AdminUserRowViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                PhoneNumber = u.PhoneNumber,
                District = u.District,
                CreatedAt = u.CreatedAt,
                IsSuspended = u.IsSuspended,
                SuspendNote = u.SuspendNote,
                IsArchived = u.IsArchived,
                Role = "Customer"
            })
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        var inventoryItems = await _context.InventoryItems
            .OrderBy(i => i.IsArchived)
            .ThenBy(i => i.Name)
            .ToListAsync();

        var inventoryTransactions = await _context.InventoryTransactions
            .Include(t => t.Item)
            .Include(t => t.PerformedBy)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var auditEntries = await _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        var loyaltyTransactions = await _context.LoyaltyTransactions.ToListAsync();

        var claims = await _context.Claims
            .Include(c => c.Order)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var customerNotes = await _context.CustomerNotes
            .Include(n => n.Author)
            .ToListAsync();

        var availableServices = await _context.LaundryServices
            .Where(s => s.IsActive)
            .OrderBy(s => s.Id)
            .ToListAsync();

        var supervisors = adminUsers.Concat(managerUsers)
            .Where(u => !u.IsSuspended && !u.IsArchived)
            .OrderBy(u => u.FullName)
            .ToList();

        var model = new AdminDashboardViewModel
        {
            CurrentUser = currentUser ?? new ApplicationUser { FullName = "Administrator" },
            Orders = orders,
            AvailableRiders = activeRiders,
            AvailableSupervisors = supervisors,
            TotalCustomersCount = allCustomers.Count,
            TotalEmployeesCount = adminUsers.Count + managerUsers.Count + staffUsers.Count + riderCount,
            Employees = employees,
            Customers = customers,
            InventoryItems = inventoryItems,
            InventoryTransactions = inventoryTransactions,
            AuditEntries = auditEntries,
            AvailableServices = availableServices,
            CustomerNotes = customerNotes,
            LoyaltyTransactions = loyaltyTransactions,
            Claims = claims
        };

        ViewData["ActiveTab"] = tab ?? "dashboard";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignPickupRider(AssignRiderInputModel input)
    {
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || 
                     Request.Headers["Accept"].ToString().Contains("application/json");

        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            if (isAjax) return Json(new { success = false, message = error });
            TempData["ErrorMessage"] = error;
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var order = await _context.LaundryOrders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == input.OrderId);

        if (order == null)
        {
            const string msg = "Order not found.";
            if (isAjax) return Json(new { success = false, message = msg });
            TempData["ErrorMessage"] = msg;
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (order.Status != OrderStatus.Pending)
        {
            const string msg = "Only pending orders can be assigned a pickup rider.";
            if (isAjax) return Json(new { success = false, message = msg });
            TempData["ErrorMessage"] = msg;
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var rider = await _userManager.FindByIdAsync(input.RiderId);
        if (rider == null || rider.IsSuspended || rider.IsArchived)
        {
            const string msg = "Selected rider is invalid, suspended, or archived.";
            if (isAjax) return Json(new { success = false, message = msg });
            TempData["ErrorMessage"] = msg;
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var isRiderRole = await _userManager.IsInRoleAsync(rider, "Rider");
        if (!isRiderRole)
        {
            const string msg = "The selected user is not designated as a Rider.";
            if (isAjax) return Json(new { success = false, message = msg });
            TempData["ErrorMessage"] = msg;
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var pickupAssignAdmin = await _userManager.GetUserAsync(User);
        var previousPickupRider = !string.IsNullOrEmpty(order.PickupRiderId)
            ? await _userManager.FindByIdAsync(order.PickupRiderId)
            : null;

        order.PickupRiderId = rider.Id;
        order.RiderAssignedAt = DateTime.UtcNow;
        order.Status = OrderStatus.RiderAssigned;
        order.UpdatedAt = DateTime.UtcNow;

        await _notificationService.NotifyAsync(rider.Id, order.Id, LaundryHub2._0.Services.NotificationService.RiderAssigned, $"New pickup job {order.OrderNumber}", $"Pickup job {order.OrderNumber}: collect from {order.Customer?.FullName ?? "customer"} — contact {order.ContactNumber}. Pick up and bring to the shop.");
        LaundryHub2._0.Services.AuditTrail.Record(_context, pickupAssignAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Assign, rider.FullName, "Rider", $"Order {order.OrderNumber} pickup leg ({previousPickupRider?.FullName ?? "Unassigned"} → {rider.FullName}).");
        await _context.SaveChangesAsync();

        var successMsg = $"Order #{order.OrderNumber} assigned to rider {rider.FullName}.";
        if (isAjax) return Json(new { success = true, message = successMsg });

        TempData["SuccessMessage"] = successMsg;
        return RedirectToAction(nameof(Index), new { tab = "orders" });
    }

    // POST /Admin/ConfirmWeight
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmWeight(int id, decimal weightKg, IFormFile? photo)
    {
        var order = await _context.LaundryOrders
            .Include(o => o.Customer)
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            TempData["ErrorMessage"] = "Order not found.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (order.Status != OrderStatus.Weighing)
        {
            TempData["ErrorMessage"] = $"Order cannot be weighed in its current status ({order.Status}).";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (weightKg <= 0)
        {
            TempData["ErrorMessage"] = "Please enter a valid weight in kilograms (greater than 0).";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        // Calculate total amount based on snapshots: sum(PricePerKgSnapshot) * weight
        var rateSum = order.OrderServices.Sum(os => os.PricePerKgSnapshot);
        if (rateSum <= 0)
        {
            TempData["ErrorMessage"] = $"Order {order.OrderNumber} has no service rate recorded, so the total cannot be calculated.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        // Save scale photo if provided
        if (photo != null && photo.Length > 0)
        {
            var validation = await LaundryHub2._0.Services.ImageUploadValidator.ValidateImageAsync(photo);
            if (!validation.IsValid)
            {
                TempData["ErrorMessage"] = validation.ErrorMessage ?? "Please upload a valid scale photo.";
                return RedirectToAction(nameof(Index), new { tab = "orders" });
            }

            var safeExtension = validation.ValidatedExtension ?? ".jpg";
            var dir = Path.Combine(_env.WebRootPath, "uploads", "orders", id.ToString());
            Directory.CreateDirectory(dir);

            // Safe cryptographically random unique server-side filename
            var fileName = $"scale_{Guid.NewGuid():N}{safeExtension}";
            var filePath = Path.Combine(dir, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await photo.CopyToAsync(stream);
            }

            order.WeightPhotoPath = $"/uploads/orders/{id}/{fileName}";
        }

        order.WeightKg = Math.Round(weightKg, 2);
        order.WeightConfirmedAt = DateTime.UtcNow;
        order.WeightConfirmationDeadline = order.WeightConfirmedAt.Value.AddHours(12);
        order.UpdatedAt = DateTime.UtcNow;

        order.TotalAmount = Math.Round(order.WeightKg.Value * rateSum, 2);

        order.Status = OrderStatus.WeightConfirmed;
        await _notificationService.NotifyAsync(order.CustomerId, order.Id, LaundryHub2._0.Services.NotificationService.WeightConfirmed, $"Order {order.OrderNumber} weighed", $"Verified weight {order.WeightKg} kg. Amount due ₱{order.TotalAmount:N2}.");
        await _notificationService.NotifyAsync(order.CustomerId, order.Id, LaundryHub2._0.Services.NotificationService.WeightConfirmRequest, $"Action needed: confirm weight for order {order.OrderNumber}", $"The shop recorded {order.WeightKg} kg (₱{order.TotalAmount:N2}). Please confirm the weight so washing can begin.");
        var weighAdmin = await _userManager.GetUserAsync(User);
        var weighServices = string.Join(", ", order.OrderServices.Select(os => os.Service?.Name ?? "?"));
        LaundryHub2._0.Services.AuditTrail.Record(_context, weighAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} weighed {order.WeightKg} kg × {rateSum}/kg = {order.TotalAmount} ({weighServices}).");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Order #{order.OrderNumber} weight confirmed: {order.WeightKg} kg. Total calculated: ₱{order.TotalAmount:N2}.";
        return RedirectToAction(nameof(Index), new { tab = "orders" });
    }

    // POST /Admin/ExtendWeightConfirmationDeadline
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtendWeightConfirmationDeadline(ExtendWeightConfirmationDeadlineInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid deadline extension input.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var order = await _context.LaundryOrders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == input.OrderId);
        if (order == null)
        {
            TempData["ErrorMessage"] = "Order not found.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (order.Status != OrderStatus.WeightConfirmed)
        {
            TempData["ErrorMessage"] = "Order is not in WeightConfirmed status.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (order.WeightConfirmedByCustomerAt.HasValue)
        {
            TempData["ErrorMessage"] = "Customer has already confirmed the weight.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var currentDeadline = order.WeightConfirmationExtensionDeadline ?? order.WeightConfirmationDeadline ?? order.WeightConfirmedAt ?? DateTime.UtcNow;
        if (input.NewDeadline <= currentDeadline)
        {
            TempData["ErrorMessage"] = "New extension deadline must be later than the current deadline.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        order.WeightConfirmationExtensionDeadline = input.NewDeadline;
        order.UpdatedAt = DateTime.UtcNow;

        var staff = await _userManager.GetUserAsync(User);
        LaundryHub2._0.Services.AuditTrail.Record(_context, staff?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} weight confirmation deadline extended to {input.NewDeadline:yyyy-MM-dd HH:mm UTC}.");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Deadline extended successfully for order #{order.OrderNumber}.";
        return RedirectToAction(nameof(Index), new { tab = "orders" });
    }

    // POST /Admin/RecordContactAttempt
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordContactAttempt(RecordContactAttemptInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid contact attempt input.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var order = await _context.LaundryOrders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == input.OrderId);
        if (order == null)
        {
            TempData["ErrorMessage"] = "Order not found.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (order.Status != OrderStatus.WeightConfirmed)
        {
            TempData["ErrorMessage"] = "Order is not in WeightConfirmed status.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var staff = await _userManager.GetUserAsync(User);
        LaundryHub2._0.Services.AuditTrail.Record(_context, staff?.FullName ?? "Unknown", "ContactAttempt", order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} contact attempt via {input.ContactMethod}: {input.Outcome}");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Contact attempt recorded for order #{order.OrderNumber}.";
        return RedirectToAction(nameof(Index), new { tab = "orders" });
    }

    // POST /Admin/PerformWeightOverride
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PerformWeightOverride(WeightOverrideInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var err = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid override input.";
            TempData["ErrorMessage"] = err;
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var order = await _context.LaundryOrders
            .Include(o => o.Customer)
            .Include(o => o.OrderServices)
            .FirstOrDefaultAsync(o => o.Id == input.OrderId);

        if (order == null)
        {
            TempData["ErrorMessage"] = "Order not found.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (order.Status != OrderStatus.WeightConfirmed)
        {
            TempData["ErrorMessage"] = "Order is not in WeightConfirmed status.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (order.WeightConfirmedByCustomerAt.HasValue)
        {
            TempData["ErrorMessage"] = "Customer has already confirmed weight.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var now = DateTime.UtcNow;
        if (!order.WeightConfirmationDeadline.HasValue || now <= order.WeightConfirmationDeadline.Value)
        {
            TempData["ErrorMessage"] = "Initial 12-hour weight confirmation deadline has not passed.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (!order.WeightConfirmationExtensionDeadline.HasValue || now <= order.WeightConfirmationExtensionDeadline.Value)
        {
            TempData["ErrorMessage"] = "Weight confirmation extension deadline has not passed or was not set.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        // Check for documented contact attempts in audit logs
        var hasContactAttempt = await _context.AuditLogs.AnyAsync(a => a.Action == "ContactAttempt" && a.Notes != null && a.Notes.Contains(order.OrderNumber));
        if (!hasContactAttempt)
        {
            TempData["ErrorMessage"] = "At least one documented customer contact attempt is required before override.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        // Verify supervisor approval (Admin or Manager)
        var supervisor = await _userManager.FindByIdAsync(input.SupervisorId);
        if (supervisor == null || supervisor.IsSuspended || supervisor.IsArchived)
        {
            TempData["ErrorMessage"] = "Selected supervisor is invalid or inactive.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var isSupervisorRole = await _userManager.IsInRoleAsync(supervisor, "Admin") || await _userManager.IsInRoleAsync(supervisor, "Manager");
        if (!isSupervisorRole)
        {
            TempData["ErrorMessage"] = "Selected user does not have supervisor (Admin/Manager) authorization.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var staff = await _userManager.GetUserAsync(User);
        if (staff == null)
        {
            TempData["ErrorMessage"] = "Staff user not found.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var rateSum = order.OrderServices.Sum(os => os.PricePerKgSnapshot);
        if (rateSum <= 0)
        {
            TempData["ErrorMessage"] = "Order has no valid service rates recorded.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        order.WeightKg = Math.Round(input.EstimatedWeight, 2);
        order.TotalAmount = Math.Round(order.WeightKg.Value * rateSum, 2);
        order.WeightOverrideByStaffId = staff.Id;
        order.WeightOverrideSupervisorId = supervisor.Id;
        order.WeightOverrideApprovedAt = now;
        order.WeightOverrideAt = now;
        order.EstimatedWeightMethod = input.EstimatedWeightMethod;
        order.WeightOverrideReason = input.WeightOverrideReason;
        order.Status = OrderStatus.AwaitingPayment;
        order.UpdatedAt = now;

        // Set immutable payment timeline anchors (only on first entry into AwaitingPayment).
        if (!order.AwaitingPaymentAt.HasValue)
        {
            order.AwaitingPaymentAt = now;
            order.PaymentDeadlineAt = now.AddHours(24);
            order.GracePeriodEndAt = now.AddHours(24 + 72); // 24h deadline + 72h grace
        }


        LaundryHub2._0.Services.AuditTrail.Record(_context, staff.FullName, "WeightOverride", order.Customer?.FullName ?? "Unknown", "Customer", 
            $"Order {order.OrderNumber} estimated-weight override: {order.WeightKg} kg (₱{order.TotalAmount}) by staff {staff.FullName}, approved by supervisor {supervisor.FullName}. Method: {input.EstimatedWeightMethod}. Reason: {input.WeightOverrideReason}");

        await _notificationService.NotifyAsync(order.CustomerId, order.Id, LaundryHub2._0.Services.NotificationService.WeightConfirmRequest, $"Order {order.OrderNumber} weight finalized", $"The shop finalized your weight at {order.WeightKg} kg (₱{order.TotalAmount:N2}). Payment is now due.");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Weight override applied for order #{order.OrderNumber}: {order.WeightKg} kg. Moved to Awaiting Payment.";
        return RedirectToAction(nameof(Index), new { tab = "orders" });
    }

    // POST /Admin/StartWashing/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartWashing(int id)
    {
        var order = await _context.LaundryOrders
            .Include(o => o.Customer)
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null || order.Status != OrderStatus.PaymentConfirmed || !order.IsPaymentConfirmed)
        {
            if (order != null)
            {
                var blockedAdmin = await _userManager.GetUserAsync(User);
                LaundryHub2._0.Services.AuditTrail.Record(_context, blockedAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} StartWashing blocked: payment not confirmed (status={order.Status}, paid={order.IsPaymentConfirmed}).");
                await _context.SaveChangesAsync();
            }
            TempData["ErrorMessage"] = "Order is not ready for washing. Payment must be confirmed first.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (!LaundryHub2._0.Services.OrderStageRequirements.RequiresWashing(order))
        {
            TempData["ErrorMessage"] = "No service on this order requires washing.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        order.Status = OrderStatus.Washing;
        order.WashingStartedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        var washAdmin = await _userManager.GetUserAsync(User);
        LaundryHub2._0.Services.AuditTrail.Record(_context, washAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} moved to Washing ({order.WeightKg} kg).");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Order #{order.OrderNumber} marked as Washing.";
        return RedirectToAction(nameof(Index), new { tab = "orders" });
    }

    // POST /Admin/MarkDrying/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDrying(int id)
    {
        var order = await _context.LaundryOrders
            .Include(o => o.Customer)
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null || order.Status != OrderStatus.Washing || !order.IsPaymentConfirmed)
        {
            if (order != null)
            {
                var blockedAdmin = await _userManager.GetUserAsync(User);
                LaundryHub2._0.Services.AuditTrail.Record(_context, blockedAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} MarkDrying blocked: payment not confirmed or not in washing stage (status={order.Status}, paid={order.IsPaymentConfirmed}).");
                await _context.SaveChangesAsync();
            }
            TempData["ErrorMessage"] = "Order is not currently in the washing stage. Payment must be confirmed first.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (!LaundryHub2._0.Services.OrderStageRequirements.RequiresDrying(order))
        {
            TempData["ErrorMessage"] = "No service on this order requires drying.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        order.Status = OrderStatus.Drying;
        order.DryingStartedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        var dryAdmin = await _userManager.GetUserAsync(User);
        LaundryHub2._0.Services.AuditTrail.Record(_context, dryAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} moved to Drying ({order.WeightKg} kg).");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Order #{order.OrderNumber} marked as Drying.";
        return RedirectToAction(nameof(Index), new { tab = "orders" });
    }

    // POST /Admin/StartDrying/{id}
    // Dry-only path: PaymentConfirmed -> Drying for orders that require drying
    // but do not require washing. Payment gate strictly enforced.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartDrying(int id)
    {
        var order = await _context.LaundryOrders
            .Include(o => o.Customer)
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null || order.Status != OrderStatus.PaymentConfirmed || !order.IsPaymentConfirmed)
        {
            if (order != null)
            {
                var blockedAdmin = await _userManager.GetUserAsync(User);
                LaundryHub2._0.Services.AuditTrail.Record(_context, blockedAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} StartDrying blocked: payment not confirmed (status={order.Status}, paid={order.IsPaymentConfirmed}).");
                await _context.SaveChangesAsync();
            }
            TempData["ErrorMessage"] = "Order is not ready for drying. Payment must be confirmed first.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (!LaundryHub2._0.Services.OrderStageRequirements.RequiresDrying(order))
        {
            TempData["ErrorMessage"] = "No service on this order requires drying.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if (LaundryHub2._0.Services.OrderStageRequirements.RequiresWashing(order))
        {
            TempData["ErrorMessage"] = "This order requires washing first. Use Start Washing.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        order.Status = OrderStatus.Drying;
        order.DryingStartedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        var dryAdmin = await _userManager.GetUserAsync(User);
        LaundryHub2._0.Services.AuditTrail.Record(_context, dryAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} moved to Drying directly (dry-only, {order.WeightKg} kg).");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Order #{order.OrderNumber} marked as Drying.";
        return RedirectToAction(nameof(Index), new { tab = "orders" });
    }

    // POST /Admin/ProcessingComplete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessingComplete(int id)
    {
        var order = await _context.LaundryOrders
            .Include(o => o.Customer)
            .Include(o => o.OrderServices)
                .ThenInclude(os => os.Service)
            .FirstOrDefaultAsync(o => o.Id == id);
        var needsDrying = order != null && LaundryHub2._0.Services.OrderStageRequirements.RequiresDrying(order);
        var needsWashing = order != null && LaundryHub2._0.Services.OrderStageRequirements.RequiresWashing(order);
        var stagesDone = order != null && (needsDrying ? order.Status == OrderStatus.Drying
            : needsWashing ? order.Status == OrderStatus.Washing
            : order.Status == OrderStatus.PaymentConfirmed);
        if (order == null || !stagesDone || !order.IsPaymentConfirmed)
        {
            if (order != null)
            {
                var blockedAdmin = await _userManager.GetUserAsync(User);
                LaundryHub2._0.Services.AuditTrail.Record(_context, blockedAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} ProcessingComplete blocked: payment not confirmed or stages incomplete (status={order.Status}, paid={order.IsPaymentConfirmed}).");
                await _context.SaveChangesAsync();
            }
            TempData["ErrorMessage"] = "Order has not finished all required processing stages. Payment must be confirmed first.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        order.Status = OrderStatus.ReadyForDelivery;
        order.ProcessingCompletedAt = DateTime.UtcNow;
        order.ReadyForDeliveryNotifiedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        var completeAdmin = await _userManager.GetUserAsync(User);
        await DeductProductionConsumptionAsync(order, completeAdmin?.Id);
        LaundryHub2._0.Services.AuditTrail.Record(_context, completeAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} processing complete ({order.WeightKg} kg, {order.TotalAmount}) — ready for delivery.");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Order #{order.OrderNumber} processing complete! Now ready for delivery.";
        return RedirectToAction(nameof(Index), new { tab = "orders" });
    }

    private async Task DeductProductionConsumptionAsync(LaundryOrder order, string? performedByUserId)
    {
        if (order.WeightKg == null || order.WeightKg <= 0)
            return;
        var detergentMlPerKg = order.OrderServices.Sum(os => os.Service?.DetergentMlPerKg ?? 0m);
        var softenerMlPerKg = order.OrderServices.Sum(os => os.Service?.SoftenerMlPerKg ?? 0m);
        if (detergentMlPerKg > 0)
            await DeductSupplyAsync(order, "Detergent", detergentMlPerKg * order.WeightKg.Value / 1000m, performedByUserId);
        if (softenerMlPerKg > 0)
            await DeductSupplyAsync(order, "Fabric Softener", softenerMlPerKg * order.WeightKg.Value / 1000m, performedByUserId);
    }

    private async Task DeductSupplyAsync(LaundryOrder order, string itemName, decimal quantity, string? performedByUserId)
    {
        var item = await _context.InventoryItems.FirstOrDefaultAsync(i => !i.IsArchived && i.Name == itemName);
        if (item == null)
            return;
        quantity = Math.Round(quantity, 2);
        var previous = item.CurrentStock;
        var deducted = Math.Min(quantity, previous);
        var shortfall = quantity - deducted;
        item.CurrentStock = Math.Round(previous - deducted, 2);
        item.UpdatedAt = DateTime.UtcNow;
        _context.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryItemId = item.Id,
            Action = InventoryStockAction.Deduct,
            Quantity = quantity,
            PreviousStock = previous,
            NewStock = item.CurrentStock,
            PerformedByUserId = performedByUserId,
            Notes = shortfall > 0
                ? $"Order {order.OrderNumber} auto-deduct {quantity} {item.Unit} (shortfall {Math.Round(shortfall, 2)} {item.Unit}; floored at zero)."
                : $"Order {order.OrderNumber} auto-deduct {quantity} {item.Unit}.",
            CreatedAt = DateTime.UtcNow
        });
    }

    // POST /Admin/AssignDeliveryRider
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignDeliveryRider(AssignRiderInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid rider assignment details.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var order = await _context.LaundryOrders.FindAsync(input.OrderId);
        if (order == null)
        {
            TempData["ErrorMessage"] = "Order not found.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        if ((order.Status != OrderStatus.ReadyForDelivery && order.Status != OrderStatus.DeliveryAttemptFailed) || !order.IsPaymentConfirmed)
        {
            var blockedAdmin = await _userManager.GetUserAsync(User);
            LaundryHub2._0.Services.AuditTrail.Record(_context, blockedAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, order.OrderNumber, "Order", $"Order {order.OrderNumber} AssignDeliveryRider blocked: payment not confirmed or not ready (status={order.Status}, paid={order.IsPaymentConfirmed}).");
            await _context.SaveChangesAsync();
            TempData["ErrorMessage"] = "Order is not ready for delivery assignment. Payment must be confirmed first.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var rider = await _userManager.FindByIdAsync(input.RiderId);
        if (rider == null || rider.IsSuspended || rider.IsArchived)
        {
            TempData["ErrorMessage"] = "Selected rider is invalid or inactive.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var isRiderRole = await _userManager.IsInRoleAsync(rider, "Rider");
        if (!isRiderRole)
        {
            TempData["ErrorMessage"] = "The selected user is not designated as a Rider.";
            return RedirectToAction(nameof(Index), new { tab = "orders" });
        }

        var deliveryAssignAdmin = await _userManager.GetUserAsync(User);
        var previousDeliveryRider = !string.IsNullOrEmpty(order.DeliveryRiderId)
            ? await _userManager.FindByIdAsync(order.DeliveryRiderId)
            : null;

        order.DeliveryRiderId = rider.Id;
        order.DeliveryAssignedAt = DateTime.UtcNow;
        order.Status = OrderStatus.OutForDelivery;
        order.UpdatedAt = DateTime.UtcNow;
        LaundryHub2._0.Services.AuditTrail.Record(_context, deliveryAssignAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Assign, rider.FullName, "Rider", $"Order {order.OrderNumber} delivery leg ({previousDeliveryRider?.FullName ?? "Unassigned"} → {rider.FullName}).");
        await _notificationService.NotifyAsync(order.CustomerId, order.Id, LaundryHub2._0.Services.NotificationService.OutForDelivery, $"Order {order.OrderNumber} out for delivery", $"Your laundry is out for delivery with {rider.FullName}.");
        await _notificationService.NotifyAsync(rider.Id, order.Id, LaundryHub2._0.Services.NotificationService.RiderAssigned, $"New delivery job {order.OrderNumber}", $"Delivery leg for order {order.OrderNumber} — customer contact {order.ContactNumber}. Deliver and capture proof photo.");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Order #{order.OrderNumber} is Out for Delivery with rider {rider.FullName}!";
        return RedirectToAction(nameof(Index), new { tab = "orders" });
    }

    // POST /Admin/ReassignRider — swaps the rider on an active pickup or
    // delivery leg. Payment state is never touched.
    // Gate: RiderReassign (Admin, Manager). Staff is forbidden.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "RiderReassign")]
    public async Task<IActionResult> ReassignRider(ReassignRiderInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var order = await _context.LaundryOrders.FindAsync(input.OrderId);
        if (order == null)
            return Json(new { success = false, message = "Order not found." });

        var isPickupLeg = order.Status == OrderStatus.RiderAssigned
            || order.Status == OrderStatus.PickedUp
            || order.Status == OrderStatus.InTransitToShop;
        var isDeliveryLeg = order.Status == OrderStatus.OutForDelivery;
        if (!isPickupLeg && !isDeliveryLeg)
            return Json(new { success = false, message = "Only orders on an active pickup or delivery leg can be reassigned." });

        var rider = await _userManager.FindByIdAsync(input.RiderId);
        if (rider == null || rider.IsSuspended || rider.IsArchived)
            return Json(new { success = false, message = "Selected rider is invalid, suspended, or archived." });

        if (!await _userManager.IsInRoleAsync(rider, "Rider"))
            return Json(new { success = false, message = "The selected user is not designated as a Rider." });

        var reassignAdmin = await _userManager.GetUserAsync(User);
        var previousRiderId = isPickupLeg ? order.PickupRiderId : order.DeliveryRiderId;
        var previousRider = !string.IsNullOrEmpty(previousRiderId)
            ? await _userManager.FindByIdAsync(previousRiderId)
            : null;

        var now = DateTime.UtcNow;
        if (isPickupLeg)
        {
            if (order.PickupRiderId == rider.Id)
                return Json(new { success = false, message = $"{rider.FullName} is already the pickup rider for this order." });
            order.PickupRiderId = rider.Id;
            order.RiderAssignedAt = now;
        }
        else
        {
            if (order.DeliveryRiderId == rider.Id)
                return Json(new { success = false, message = $"{rider.FullName} is already the delivery rider for this order." });
            order.DeliveryRiderId = rider.Id;
            order.DeliveryAssignedAt = now;
        }
        order.UpdatedAt = now;
        var leg = isPickupLeg ? "pickup" : "delivery";
        await _notificationService.NotifyAsync(rider.Id, order.Id, LaundryHub2._0.Services.NotificationService.RiderAssigned, $"Reassigned {leg} job {order.OrderNumber}", $"{(isPickupLeg ? "Pickup" : "Delivery")} leg for order {order.OrderNumber} reassigned to you — customer contact {order.ContactNumber}.");
        LaundryHub2._0.Services.AuditTrail.Record(_context, reassignAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Reassign, rider.FullName, "Rider", $"Order {order.OrderNumber} {(isPickupLeg ? "pickup" : "delivery")} leg ({previousRider?.FullName ?? "Unassigned"} → {rider.FullName}).");
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"Order {order.OrderNumber} reassigned to {rider.FullName}." });
    }

    // POST /Admin/CreateWalkInOrder — counter drop-off entered by staff. The shop
    // holds the bag, so the order starts at Weighing and never has pickup legs.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateWalkInOrder(CreateWalkInOrderInputModel input)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            return StatusCode(403, new { success = false, message = "Access denied. Only Admins and Managers can create walk-in orders." });

        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var customer = await _userManager.FindByIdAsync(input.CustomerId);
        if (customer == null)
            return Json(new { success = false, message = "Customer not found." });

        if (customer.IsSuspended || customer.IsArchived)
            return Json(new { success = false, message = "The customer account is not active." });

        if (input.ServiceIds == null || input.ServiceIds.Count == 0)
            return Json(new { success = false, message = "Please select at least one laundry service." });

        var serviceIds = input.ServiceIds.Distinct().ToList();
        var services = await _context.LaundryServices
            .Where(s => serviceIds.Contains(s.Id) && s.IsActive)
            .ToListAsync();
        if (services.Count != serviceIds.Count)
            return Json(new { success = false, message = "One or more selected services are invalid or inactive." });

        if (!input.AcceptTerms)
            return Json(new { success = false, message = "You must agree to the Terms & Conditions to create a walk-in order." });

        LaundryOrder order;
        try
        {
            order = await _orderNumberService.CreateOrderWithUniqueNumberAsync(orderNumber => new LaundryOrder
            {
                OrderNumber = orderNumber,
                CustomerId = customer.Id,
                Origin = OrderOrigin.WalkIn,
                PreferredPickupDate = DateTime.UtcNow.Date,
                PreferredPickupTime = "Walk-in",
                PickupLocation = "Walk-in counter",
                ContactNumber = customer.PhoneNumber ?? string.Empty,
                Status = OrderStatus.Weighing,
                CreatedAt = DateTime.UtcNow,
                TermsAcceptedAt = DateTime.UtcNow,
                TermsVersion = LaundryOrder.CurrentTermsVersion
            });
        }
        catch (LaundryHub2._0.Services.DuplicateOrderNumberException)
        {
            return Json(new { success = false, message = "Could not reserve an order number after 3 attempts. Please try again — no order was created." });
        }

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

        var actingAdmin = await _userManager.GetUserAsync(User);
        var svcNames = string.Join(", ", services.Select(s => s.Name));
        LaundryHub2._0.Services.AuditTrail.Record(_context, actingAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Add, customer.FullName, "Customer", $"Walk-in order {order.OrderNumber} created ({svcNames}).");
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"Walk-in order {order.OrderNumber} created and ready for weighing.", orderNumber = order.OrderNumber });
    }

    // POST /Admin/AddCustomerNote — append-only staff note on a customer profile.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomerNote(AddCustomerNoteInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var customer = await _userManager.FindByIdAsync(input.CustomerId);
        if (customer == null)
            return Json(new { success = false, message = "Customer not found." });

        var text = input.Text.Trim();
        if (string.IsNullOrEmpty(text))
            return Json(new { success = false, message = "Note text is required." });

        var author = await _userManager.GetUserAsync(User);
        _context.CustomerNotes.Add(new CustomerNote
        {
            CustomerId = customer.Id,
            AuthorUserId = author?.Id ?? string.Empty,
            Text = text,
            CreatedAt = DateTime.UtcNow
        });
        LaundryHub2._0.Services.AuditTrail.Record(_context, author?.FullName ?? "Unknown", "Note", customer.FullName, "Customer", $"Note on {customer.FullName}: {(text.Length <= 400 ? text : text.Substring(0, 400) + "…")}");
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Note added." });
    }

    // POST /Admin/SendDormantPromo — Manager+ reactivation message blast to dormant
    // customers. Message only: one Promo notification per customer plus one audit
    // entry. All-or-nothing per request; one promo per customer per 7 days.
    // Gate: Admin, Manager. Staff should not initiate customer marketing campaigns.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendDormantPromo(SendDormantPromoInputModel input)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager"))
            return StatusCode(403, new { success = false, message = "Access denied. Only Admins and Managers can send promotional messages." });

        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var title = input.Title.Trim();
        var body = input.Body.Trim();
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body))
            return Json(new { success = false, message = "Title and body are required." });

        var ids = (input.CustomerIds ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return Json(new { success = false, message = "Select at least one customer." });

        // Validate every ID before writing anything — invalid ID aborts the whole request.
        var customers = new List<ApplicationUser>();
        foreach (var id in ids)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.IsArchived)
                return Json(new { success = false, message = $"Customer not found or archived: {id}. No promos sent." });
            if (!await _userManager.IsInRoleAsync(user, "Customer"))
                return Json(new { success = false, message = $"Not a customer account: {id}. No promos sent." });
            customers.Add(user);
        }

        var cutoff = DateTime.UtcNow.AddDays(-7);
        var recent = await _context.Notifications
            .Where(n => n.Type == LaundryHub2._0.Services.NotificationService.Promo
                && ids.Contains(n.RecipientUserId)
                && n.CreatedAt >= cutoff)
            .Select(n => n.RecipientUserId)
            .Distinct()
            .ToListAsync();
        if (recent.Count > 0)
            return Json(new { success = false, message = $"{recent.Count} customer(s) already received a promo in the last 7 days — no promos sent." });

        var now = DateTime.UtcNow;
        foreach (var customer in customers)
        {
            _context.Notifications.Add(new Notification
            {
                RecipientUserId = customer.Id,
                OrderId = null,
                Type = LaundryHub2._0.Services.NotificationService.Promo,
                Title = title,
                Body = body,
                IsRead = false,
                CreatedAt = now
            });
        }
        var author = await _userManager.GetUserAsync(User);
        LaundryHub2._0.Services.AuditTrail.Record(_context, author?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Promo, $"{customers.Count} dormant customers", "Customer", $"Promo '{title}' sent to {customers.Count} dormant customer(s) by {author?.FullName ?? "Unknown"}.");
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"Promo sent to {customers.Count} customer(s)." });
    }

    // POST /Admin/AdjustLoyaltyPoints — manual signed adjustment to a
    // customer's loyalty balance. No order linkage, no expiry.
    // Gate: LoyaltyAdjust (Admin only). Managers and Staff are forbidden.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "LoyaltyAdjust")]
    public async Task<IActionResult> AdjustLoyaltyPoints(AdjustLoyaltyPointsInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var customer = await _userManager.FindByIdAsync(input.CustomerId);
        if (customer == null)
            return Json(new { success = false, message = "Customer not found." });

        if (input.Points == 0)
            return Json(new { success = false, message = "Points must not be zero." });

        var reason = input.Reason.Trim();
        if (string.IsNullOrEmpty(reason))
            return Json(new { success = false, message = "Reason is required." });

        _context.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            CustomerId = customer.Id,
            OrderId = null,
            Type = LoyaltyTransactionType.Adjust,
            Points = input.Points,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        });

        var actingAdmin = await _userManager.GetUserAsync(User);
        var customerRoles = await _userManager.GetRolesAsync(customer);
        LaundryHub2._0.Services.AuditTrail.Record(_context, actingAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Adjust, customer.FullName, customerRoles.FirstOrDefault() ?? "Unknown", reason);
        await _context.SaveChangesAsync();

        var balance = await _loyaltyService.GetBalanceAsync(customer.Id);
        return Json(new { success = true, message = $"Adjusted {customer.FullName} by {input.Points} points.", balance });
    }

    // POST /Admin/RefundOrder — refunds a paid, undelivered order through the
    // PayMongo refunds API. The gateway is called first; order state changes
    // only on gateway success. Manual handling stays outside the system.
    // Gate: Refunds (Admin only). Managers and Staff are forbidden.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "Refunds")]
    public async Task<IActionResult> RefundOrder(RefundOrderInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var order = await _context.LaundryOrders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == input.OrderId);
        if (order == null)
            return Json(new { success = false, message = "Order not found." });

        if (!order.IsPaymentConfirmed)
            return Json(new { success = false, message = "Only paid orders can be refunded." });

        if (order.Status == OrderStatus.Delivered)
            return Json(new { success = false, message = "Delivered orders cannot be refunded." });

        if (order.RefundAmount.HasValue)
            return Json(new { success = false, message = "This order has already been refunded." });

        var refundable = await _loyaltyService.ComputeAmountDueAsync(order);
        if (refundable <= 0)
            return Json(new { success = false, message = "There is no refundable amount on this order." });

        var verification = await _payMongoService.VerifySessionPaidAsync(
            order.PayMongoPaymentId ?? string.Empty, refundable);
        if (!verification.Paid || string.IsNullOrEmpty(verification.PaidPaymentId))
            return Json(new { success = false, message = "The original payment could not be verified with the gateway. No refund was issued." });

        var refund = await _payMongoService.RefundPaymentAsync(
            verification.PaidPaymentId,
            (long)Math.Round(refundable * 100m, MidpointRounding.AwayFromZero));
        if (!refund.Succeeded)
            return Json(new { success = false, message = refund.Reason });

        var now = DateTime.UtcNow;
        order.Status = OrderStatus.Cancelled;
        order.RefundAmount = Math.Round(refundable, 2);
        order.RefundedAt = now;
        order.UpdatedAt = now;
        var redeemedPoints = await _context.LoyaltyTransactions
            .Where(t => t.OrderId == order.Id && t.Type == LaundryHub2._0.Models.LoyaltyTransactionType.Redeem)
            .SumAsync(t => (int?)t.Points) ?? 0;
        var returnedPoints = redeemedPoints < 0 ? -redeemedPoints : 0;
        if (returnedPoints > 0)
        {
            _context.LoyaltyTransactions.Add(new LaundryHub2._0.Models.LoyaltyTransaction
            {
                CustomerId = order.CustomerId,
                OrderId = order.Id,
                Type = LaundryHub2._0.Models.LoyaltyTransactionType.Earn,
                Points = returnedPoints,
                Reason = $"Refunded order {order.OrderNumber}: returned {returnedPoints} points",
                ExpiresAt = now.AddMonths(12),
                CreatedAt = now
            });
        }
        var actingAdmin = await _userManager.GetUserAsync(User);
        LaundryHub2._0.Services.AuditTrail.Record(_context, actingAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Refund, order.Customer?.FullName ?? "Unknown", "Customer", returnedPoints > 0 ? $"Order {order.OrderNumber} refunded ₱{order.RefundAmount:N2}. Returned {returnedPoints} loyalty points." : $"Order {order.OrderNumber} refunded ₱{order.RefundAmount:N2}.");
        await _notificationService.NotifyAsync(order.CustomerId, order.Id, LaundryHub2._0.Services.NotificationService.RefundIssued, $"Refund issued for order {order.OrderNumber}", $"A refund of ₱{order.RefundAmount:N2} was issued to your original payment method." + (returnedPoints > 0 ? $" Returned {returnedPoints} loyalty points to your balance." : string.Empty));
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"Order {order.OrderNumber} refunded ₱{order.RefundAmount:N2}.", refundId = refund.RefundId });
    }

    // POST /Admin/FileClaim — staff-filed damage/loss record on an order.
    // Append-only: claims are resolved, never edited or deleted.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FileClaim(FileClaimInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var orderNumber = input.OrderNumber.Trim();
        if (string.IsNullOrEmpty(orderNumber))
            return Json(new { success = false, message = "Order number is required." });

        var order = await _context.LaundryOrders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
        if (order == null)
            return Json(new { success = false, message = "Order not found." });

        if (input.Type != ClaimType.Damage && input.Type != ClaimType.Loss && input.Type != ClaimType.Other)
            return Json(new { success = false, message = "Claim type must be Damage, Loss, or Other." });

        var description = input.Description.Trim();
        if (string.IsNullOrEmpty(description))
            return Json(new { success = false, message = "Description is required." });

        var reporter = await _userManager.GetUserAsync(User);
        _context.Claims.Add(new Claim
        {
            OrderId = order.Id,
            ReporterName = reporter?.FullName ?? "Unknown staff",
            ReporterIsStaff = true,
            Type = input.Type,
            Description = description,
            Status = ClaimStatus.Open,
            CreatedAt = DateTime.UtcNow
        });
        LaundryHub2._0.Services.AuditTrail.Record(_context, reporter?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Claim, order.Customer?.FullName ?? "Unknown", "Customer", $"{input.Type} claim filed on order {order.OrderNumber} by {reporter?.FullName ?? "Unknown staff"}.");
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"{input.Type} claim filed on order {order.OrderNumber}." });
    }

    // POST /Admin/ResolveClaim — moves an Open claim to Resolved or Rejected.
    // Recording only: rewash orders and refunds run through their own flows.
    // Gate: ClaimsResolve (Admin, Manager). Filing stays Staff+; only
    // resolution is tiered, so Staff is forbidden here.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "ClaimsResolve")]
    public async Task<IActionResult> ResolveClaim(ResolveClaimInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var claim = await _context.Claims
            .Include(c => c.Order)
            .FirstOrDefaultAsync(c => c.Id == input.ClaimId);
        if (claim == null)
            return Json(new { success = false, message = "Claim not found." });

        if (claim.Status != ClaimStatus.Open)
            return Json(new { success = false, message = "Only open claims can be resolved." });

        if (input.Status != ClaimStatus.Resolved && input.Status != ClaimStatus.Rejected)
            return Json(new { success = false, message = "Resolution must be Resolved or Rejected." });

        var note = input.ResolutionNote.Trim();
        if (string.IsNullOrEmpty(note))
            return Json(new { success = false, message = "Resolution note is required." });

        claim.Status = input.Status;
        claim.ResolutionNote = note;
        claim.ResolvedAt = DateTime.UtcNow;
        var resolver = await _userManager.GetUserAsync(User);
        LaundryHub2._0.Services.AuditTrail.Record(_context, resolver?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Claim, claim.Order?.OrderNumber ?? $"order #{claim.OrderId}", "Order", $"Claim #{claim.Id} {input.Status.ToLowerInvariant()}: {note}");
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"Claim #{claim.Id} marked as {input.Status}." });
    }

    // POST /Admin/AddInventoryItem — creates a new inventory catalog item.
    // Gate: InventoryManagement (Admin, Manager). Staff may only adjust existing stock.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> AddInventoryItem(InventoryItemInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid inventory item.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }

        var name = input.Name.Trim();
        var unit = input.Unit.Trim();
        if (await _context.InventoryItems.AnyAsync(i => !i.IsArchived && i.Name.ToLower() == name.ToLower()))
        {
            TempData["ErrorMessage"] = $"An active inventory item named '{name}' already exists.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }

        var now = DateTime.UtcNow;
        var item = new InventoryItem
        {
            Name = name,
            Unit = unit,
            CurrentStock = Math.Round(input.CurrentStock, 2),
            MaxCapacity = Math.Round(input.MaxCapacity, 2),
            LowStockThreshold = Math.Round(input.LowStockThreshold, 2),
            CreatedAt = now
        };
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        if (item.CurrentStock > 0)
        {
            var user = await _userManager.GetUserAsync(User);
            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                InventoryItemId = item.Id,
                Action = InventoryStockAction.Add,
                Quantity = item.CurrentStock,
                PreviousStock = 0m,
                NewStock = item.CurrentStock,
                PerformedByUserId = user?.Id,
                Notes = "Initial stock",
                CreatedAt = now
            });
            await _context.SaveChangesAsync();
        }

        TempData["SuccessMessage"] = $"Inventory item '{name}' was added.";
        return RedirectToAction(nameof(Index), new { tab = "inventory" });
    }

    // Gate: InventoryManagement (Admin, Manager).
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> EditInventoryItem(int id, InventoryItemInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid inventory item.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }

        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null)
        {
            TempData["ErrorMessage"] = "Inventory item not found.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }

        var name = input.Name.Trim();
        var unit = input.Unit.Trim();
        var maxCapacity = Math.Round(input.MaxCapacity, 2);
        var threshold = Math.Round(input.LowStockThreshold, 2);
        if (await _context.InventoryItems.AnyAsync(i => i.Id != id && !i.IsArchived && i.Name.ToLower() == name.ToLower()))
        {
            TempData["ErrorMessage"] = $"An active inventory item named '{name}' already exists.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }
        if (maxCapacity < item.CurrentStock)
        {
            TempData["ErrorMessage"] = "Max capacity cannot be below current stock. Deduct stock first.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }

        item.Name = name;
        item.Unit = unit;
        item.MaxCapacity = maxCapacity;
        item.LowStockThreshold = threshold;
        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Inventory item '{name}' was updated.";
        return RedirectToAction(nameof(Index), new { tab = "inventory" });
    }

    // Gate: InventoryManagement (Admin, Manager).
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> SetInventoryItemArchived(int id, bool isArchived)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null)
        {
            TempData["ErrorMessage"] = "Inventory item not found.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }

        item.IsArchived = isArchived;
        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = isArchived ? $"Inventory item '{item.Name}' was archived." : $"Inventory item '{item.Name}' was restored.";
        return RedirectToAction(nameof(Index), new { tab = "inventory" });
    }

    // Gate: InventoryManagement (Admin, Manager).
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "InventoryManagement")]
    public async Task<IActionResult> DeleteInventoryItem(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null)
        {
            TempData["ErrorMessage"] = "Inventory item not found.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }
        if (await _context.InventoryTransactions.AnyAsync(t => t.InventoryItemId == id))
        {
            TempData["ErrorMessage"] = "This item has stock history. Archive it instead of deleting it.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }

        _context.InventoryItems.Remove(item);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Inventory item '{item.Name}' was deleted.";
        return RedirectToAction(nameof(Index), new { tab = "inventory" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustInventoryStock(InventoryStockAdjustmentInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid stock adjustment.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }

        var item = await _context.InventoryItems.FindAsync(input.ItemId);
        if (item == null || item.IsArchived)
        {
            TempData["ErrorMessage"] = "Inventory item not found or archived.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }

        var quantity = Math.Round(input.Quantity, 2);
        var previous = item.CurrentStock;
        var updated = input.Action == InventoryStockAction.Add ? previous + quantity : previous - quantity;
        updated = Math.Round(updated, 2);
        if (updated < 0)
        {
            TempData["ErrorMessage"] = $"Cannot deduct {quantity} {item.Unit}. Only {previous} {item.Unit} available.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }
        if (updated > item.MaxCapacity)
        {
            TempData["ErrorMessage"] = $"Cannot add {quantity} {item.Unit}. Max capacity is {item.MaxCapacity} {item.Unit}.";
            return RedirectToAction(nameof(Index), new { tab = "inventory" });
        }

        var user = await _userManager.GetUserAsync(User);
        var now = DateTime.UtcNow;
        item.CurrentStock = updated;
        item.UpdatedAt = now;
        _context.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryItemId = item.Id,
            Action = input.Action,
            Quantity = quantity,
            PreviousStock = previous,
            NewStock = updated,
            PerformedByUserId = user?.Id,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
            CreatedAt = now
        });
        await _context.SaveChangesAsync();

        var verb = input.Action == InventoryStockAction.Add ? "added to" : "deducted from";
        TempData["SuccessMessage"] = $"{quantity} {item.Unit} {verb} '{item.Name}'. New stock: {updated} {item.Unit}.";
        return RedirectToAction(nameof(Index), new { tab = "inventory" });
    }

    // POST /Admin/SuspendUser — toggles IsSuspended on a real Identity user.
    // Called via fetch from the Employee/Customer tables; always returns JSON
    // with an explicit success flag and message (no silent failures).
    // Gate: UserManagement (Admin, Manager). Managers may not suspend Admins.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "UserManagement")]
    public async Task<IActionResult> SuspendUser(SuspendUserInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var note = input.SuspendNote?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(note))
            return Json(new { success = false, message = "A suspension reason is required." });

        var target = await _userManager.FindByIdAsync(input.UserId);
        if (target == null)
            return Json(new { success = false, message = "User not found." });

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser != null && target.Id == currentUser.Id)
            return Json(new { success = false, message = "You cannot suspend your own account." });

        var targetRoles = await _userManager.GetRolesAsync(target);
        if (targetRoles.Contains("Admin") && !User.IsInRole("Admin"))
            return StatusCode(403, new { success = false, message = "Access denied. Only Admins can suspend Admin accounts." });

        if (targetRoles.Contains("Admin"))
        {
            var otherUsableAdmins = await _userManager.GetUsersInRoleAsync("Admin");
            if (!otherUsableAdmins.Any(u => u.Id != target.Id && !u.IsSuspended && !u.IsArchived))
                return Json(new { success = false, message = "Cannot suspend the last usable Admin. Promote another account to Admin first." });
        }

        target.IsSuspended = true;
        target.SuspendNote = note;
        LaundryHub2._0.Services.AuditTrail.Record(_context, currentUser?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Suspend, target.FullName, targetRoles.FirstOrDefault() ?? "Unknown", note);
        var result = await _userManager.UpdateAsync(target);
        if (!result.Succeeded)
            return Json(new { success = false, message = result.Errors.FirstOrDefault()?.Description ?? "Failed to suspend the account." });

        // Bump the security stamp so the suspended user's existing sessions are
        // invalidated at the next stamp check instead of lingering.
        await _userManager.UpdateSecurityStampAsync(target);

        return Json(new { success = true, message = $"{target.FullName} has been suspended." });
    }

    // POST /Admin/UnsuspendUser — clears IsSuspended on a real Identity user.
    // Gate: UserManagement (Admin, Manager).
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "UserManagement")]
    public async Task<IActionResult> UnsuspendUser(UnsuspendUserInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var target = await _userManager.FindByIdAsync(input.UserId);
        if (target == null)
            return Json(new { success = false, message = "User not found." });

        target.IsSuspended = false;
        target.SuspendNote = null;
        var unsuspendCurrentUser = await _userManager.GetUserAsync(User);
        var unsuspendTargetRoles = await _userManager.GetRolesAsync(target);
        LaundryHub2._0.Services.AuditTrail.Record(_context, unsuspendCurrentUser?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Unsuspend, target.FullName, unsuspendTargetRoles.FirstOrDefault() ?? "Unknown", null);
        var result = await _userManager.UpdateAsync(target);
        if (!result.Succeeded)
            return Json(new { success = false, message = result.Errors.FirstOrDefault()?.Description ?? "Failed to unsuspend the account." });

        await _userManager.UpdateSecurityStampAsync(target);

        return Json(new { success = true, message = $"{target.FullName} has been unsuspended." });
    }

    // POST /Admin/CreateUser — creates a real Identity user (password hashed by
    // UserManager) with a single role. Called via fetch; always returns JSON.
    // Gate: UserManagement (Admin, Manager). Only Admins may assign Admin or
    // Manager roles (enforced below).
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "UserManagement")]
    public async Task<IActionResult> CreateUser(CreateUserInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var email = input.Email.Trim();
        var name = input.FullName.Trim();
        if (!Data.DbInitializer.Roles.Contains(input.Role))
            return Json(new { success = false, message = $"Unknown role '{input.Role}'." });

        // Privilege tiers: Managers may create Staff/Rider/Customer accounts,
        // but only Admins may mint Admin or Manager accounts.
        if (!User.IsInRole("Admin") && (input.Role == "Admin" || input.Role == "Manager"))
            return StatusCode(403, new { success = false, message = "Only Admins can assign Admin or Manager roles." });

        if (await _userManager.FindByEmailAsync(email) != null)
            return Json(new { success = false, message = "An account with that email already exists." });

        var note = input.SuspendNote?.Trim() ?? string.Empty;
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = name,
            CreatedAt = DateTime.UtcNow,
            IsSuspended = !string.IsNullOrEmpty(note),
            SuspendNote = string.IsNullOrEmpty(note) ? null : note
        };

        var result = await _userManager.CreateAsync(user, input.Password);
        if (!result.Succeeded)
            return Json(new { success = false, message = result.Errors.FirstOrDefault()?.Description ?? "Failed to create the account." });

        var roleResult = await _userManager.AddToRoleAsync(user, input.Role);
        if (!roleResult.Succeeded)
            return Json(new { success = false, message = "Account created, but role assignment failed: " + (roleResult.Errors.FirstOrDefault()?.Description ?? "unknown error.") });

        var actingAdmin = await _userManager.GetUserAsync(User);
        LaundryHub2._0.Services.AuditTrail.Record(_context, actingAdmin?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Add, name, input.Role, string.IsNullOrEmpty(note) ? null : note);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"Account created for {name} ({input.Role})." });
    }

    // POST /Admin/EditUser — updates FullName, Email (kept in sync with
    // UserName, as with seeded accounts) and single role. Called via fetch.
    // Gate: UserManagement (Admin, Manager). Only Admins may assign Admin or
    // Manager roles or demote Admins (enforced below).
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "UserManagement")]
    public async Task<IActionResult> EditUser(EditUserInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var target = await _userManager.FindByIdAsync(input.UserId);
        if (target == null)
            return Json(new { success = false, message = "User not found." });

        var email = input.Email.Trim();
        if (!Data.DbInitializer.Roles.Contains(input.Role))
            return Json(new { success = false, message = $"Unknown role '{input.Role}'." });

        var currentRoles = await _userManager.GetRolesAsync(target);
        var roleWillChange = currentRoles.Count != 1 || currentRoles[0] != input.Role;

        // Privilege tiers: only Admins may assign Admin or Manager roles.
        if (roleWillChange && !User.IsInRole("Admin") && (input.Role == "Admin" || input.Role == "Manager"))
            return StatusCode(403, new { success = false, message = "Only Admins can assign Admin or Manager roles." });

        // Privilege tiers: Managers may not demote Admins to a lesser role.
        if (roleWillChange && currentRoles.Contains("Admin") && input.Role != "Admin" && !User.IsInRole("Admin"))
            return StatusCode(403, new { success = false, message = "Access denied. Only Admins can demote Admin accounts." });

        var emailOwner = await _userManager.FindByEmailAsync(email);
        if (emailOwner != null && emailOwner.Id != target.Id)
            return Json(new { success = false, message = "An account with that email already exists." });

        // Nobody may change their own role (prevents self-demotion lockouts
        // and self-promotion). Name/email edits on your own account stay allowed.
        var currentUser = await _userManager.GetUserAsync(User);
        if (roleWillChange && currentUser != null && target.Id == currentUser.Id)
            return Json(new { success = false, message = "You cannot change your own role." });

        if (roleWillChange && currentRoles.Contains("Admin") && input.Role != "Admin")
        {
            var remainingAdmins = await _userManager.GetUsersInRoleAsync("Admin");
            if (!remainingAdmins.Any(u => u.Id != target.Id && !u.IsSuspended && !u.IsArchived))
                return Json(new { success = false, message = "Cannot remove the last Admin. Promote another account to Admin first." });
        }

        target.FullName = input.FullName.Trim();
        if (!string.Equals(target.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _userManager.SetEmailAsync(target, email);
            if (!emailResult.Succeeded)
                return Json(new { success = false, message = emailResult.Errors.FirstOrDefault()?.Description ?? "Failed to update the email." });
            var nameResult = await _userManager.SetUserNameAsync(target, email);
            if (!nameResult.Succeeded)
                return Json(new { success = false, message = nameResult.Errors.FirstOrDefault()?.Description ?? "Failed to update the username." });
        }
        else
        {
            var updateResult = await _userManager.UpdateAsync(target);
            if (!updateResult.Succeeded)
                return Json(new { success = false, message = updateResult.Errors.FirstOrDefault()?.Description ?? "Failed to update the account." });
        }

        if (roleWillChange)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(target, currentRoles);
            if (!removeResult.Succeeded)
                return Json(new { success = false, message = removeResult.Errors.FirstOrDefault()?.Description ?? "Failed to change the role." });
            var addResult = await _userManager.AddToRoleAsync(target, input.Role);
            if (!addResult.Succeeded)
                return Json(new { success = false, message = addResult.Errors.FirstOrDefault()?.Description ?? "Failed to assign the new role." });
        }

        var roleNote = roleWillChange ? $"{currentRoles.FirstOrDefault() ?? "Unknown"} → {input.Role}" : null;
        LaundryHub2._0.Services.AuditTrail.Record(_context, currentUser?.FullName ?? "Unknown", LaundryHub2._0.Services.AuditTrail.Update, target.FullName, input.Role, roleNote);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = $"{target.FullName} has been updated." });
    }

    // POST /Admin/SetUserArchived — toggles IsArchived. Archived accounts stay
    // in the database (and order history) and remain visible in the tables
    // with an Archived badge until restored.
    // Gate: UserManagement (Admin, Manager). Managers may not archive Admins.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "UserManagement")]
    public async Task<IActionResult> SetUserArchived(SetUserArchivedInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid input.";
            return Json(new { success = false, message = error });
        }

        var target = await _userManager.FindByIdAsync(input.UserId);
        if (target == null)
            return Json(new { success = false, message = "User not found." });

        var currentUser = await _userManager.GetUserAsync(User);
        if (input.IsArchived && currentUser != null && target.Id == currentUser.Id)
            return Json(new { success = false, message = "You cannot archive your own account." });

        if (input.IsArchived && !User.IsInRole("Admin"))
        {
            var preArchiveRoles = await _userManager.GetRolesAsync(target);
            if (preArchiveRoles.Contains("Admin"))
                return StatusCode(403, new { success = false, message = "Access denied. Only Admins can archive Admin accounts." });
        }

        if (input.IsArchived)
        {
            var targetRoles = await _userManager.GetRolesAsync(target);
            if (targetRoles.Contains("Admin"))
            {
                var otherUsableAdmins = await _userManager.GetUsersInRoleAsync("Admin");
                if (!otherUsableAdmins.Any(u => u.Id != target.Id && !u.IsSuspended && !u.IsArchived))
                    return Json(new { success = false, message = "Cannot archive the last usable Admin. Promote another account to Admin first." });
            }
        }

        target.IsArchived = input.IsArchived;
        var archiveTargetRoles = await _userManager.GetRolesAsync(target);
        LaundryHub2._0.Services.AuditTrail.Record(_context, currentUser?.FullName ?? "Unknown", input.IsArchived ? LaundryHub2._0.Services.AuditTrail.Archive : LaundryHub2._0.Services.AuditTrail.Restore, target.FullName, archiveTargetRoles.FirstOrDefault() ?? "Unknown", null);
        var result = await _userManager.UpdateAsync(target);
        if (!result.Succeeded)
            return Json(new { success = false, message = result.Errors.FirstOrDefault()?.Description ?? "Failed to update the account." });

        // Same session invalidation as suspend: an archived account must lose
        // access immediately, not at the next cookie expiry.
        await _userManager.UpdateSecurityStampAsync(target);

        return Json(new { success = true, message = input.IsArchived ? $"{target.FullName} has been archived." : $"{target.FullName} has been restored." });
    }

    // POST /Admin/RunPaymentDeadlineBackfill
    // One-time explicit maintenance action: backfills missing AwaitingPaymentAt/PaymentDeadlineAt/GracePeriodEndAt
    // on orders currently in AwaitingPayment without changing existing rules or payment state.
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunPaymentDeadlineBackfill()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var executedBy = currentUser?.FullName ?? "Administrator";

        var result = await _backfillService.ExecuteBackfillAsync(executedBy);

        return Json(new
        {
            success = true,
            totalInspected = result.TotalAwaitingPaymentInspected,
            alreadySetCount = result.AlreadySetCount,
            backfilledCount = result.SuccessfullyBackfilledCount,
            skippedCount = result.SkippedCount,
            details = result.Details,
            excludedOrders = result.ExcludedOrdersReport
        });
    }

    /// <summary>
    /// Phase 2F: Returns currently active rider GPS dispatch locations for active tracking orders.
    /// Strictly restricted to Admin and Manager roles. Staff cannot access.
    /// Pickup active states: RiderAssigned, PickedUp, InTransitToShop.
    /// Delivery active states: ReadyForDelivery, OutForDelivery, DeliveryAttemptFailed.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetActiveRiderLocations()
    {
        // 1. Fetch active orders with their assigned riders
        var activeOrders = await _context.LaundryOrders
            .AsNoTracking()
            .Include(o => o.PickupRider)
            .Include(o => o.DeliveryRider)
            .Where(o =>
                (o.PickupRiderId != null &&
                 (o.Status == OrderStatus.RiderAssigned ||
                  o.Status == OrderStatus.PickedUp ||
                  o.Status == OrderStatus.InTransitToShop))
                ||
                (o.DeliveryRiderId != null &&
                 (o.Status == OrderStatus.ReadyForDelivery ||
                  o.Status == OrderStatus.OutForDelivery ||
                  o.Status == OrderStatus.DeliveryAttemptFailed)))
            .ToListAsync();

        var dispatchList = new List<ActiveRiderLocationDto>();

        foreach (var order in activeOrders)
        {
            string? riderId = null;
            string? riderName = null;
            decimal? riderLat = null;
            decimal? riderLng = null;
            DateTime? riderUpdated = null;
            string trackingType = "none";

            if (order.Status == OrderStatus.RiderAssigned ||
                order.Status == OrderStatus.PickedUp ||
                order.Status == OrderStatus.InTransitToShop)
            {
                trackingType = "pickup";
                riderId = order.PickupRiderId;
                riderName = order.PickupRider?.FullName;
                riderLat = order.PickupRider?.CurrentLatitude;
                riderLng = order.PickupRider?.CurrentLongitude;
                riderUpdated = order.PickupRider?.LastLocationUpdatedAt;
            }
            else if (order.Status == OrderStatus.ReadyForDelivery ||
                     order.Status == OrderStatus.OutForDelivery ||
                     order.Status == OrderStatus.DeliveryAttemptFailed)
            {
                trackingType = "delivery";
                riderId = order.DeliveryRiderId;
                riderName = order.DeliveryRider?.FullName;
                riderLat = order.DeliveryRider?.CurrentLatitude;
                riderLng = order.DeliveryRider?.CurrentLongitude;
                riderUpdated = order.DeliveryRider?.LastLocationUpdatedAt;
            }

            if (string.IsNullOrEmpty(riderId)) continue;

            dispatchList.Add(new ActiveRiderLocationDto
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                RiderId = riderId,
                RiderName = riderName ?? "Rider",
                Latitude = riderLat,
                Longitude = riderLng,
                UpdatedAt = riderUpdated,
                TrackingType = trackingType,
                Status = order.Status.ToString(),
                OrderLatitude = order.PickupLatitude,
                OrderLongitude = order.PickupLongitude
            });
        }

        return Json(new { success = true, data = dispatchList });
    }
}

