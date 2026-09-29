using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Data;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Controllers;

[Authorize(Roles = "Rider,Admin")]
public class RiderController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;
    private readonly LaundryHub2._0.Services.NotificationService _notificationService;

    public RiderController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env,
        LaundryHub2._0.Services.NotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _env = env;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? tab = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Account");

        var myJobs = await _context.LaundryOrders
            .Include(o => o.Customer)
            .Include(o => o.OrderServices).ThenInclude(os => os.Service)
            .Where(o => (o.PickupRiderId == user.Id &&
                         (o.Status == OrderStatus.RiderAssigned ||
                          o.Status == OrderStatus.PickedUp ||
                          o.Status == OrderStatus.InTransitToShop)) ||
                        (o.DeliveryRiderId == user.Id &&
                          o.Status == OrderStatus.OutForDelivery))
            .OrderByDescending(o => o.DeliveryAssignedAt ?? o.RiderAssignedAt)
            .ToListAsync();

        var history = await _context.LaundryOrders
            .Include(o => o.Customer)
            .Include(o => o.OrderServices).ThenInclude(os => os.Service)
            .Where(o => (o.PickupRiderId == user.Id || o.DeliveryRiderId == user.Id) &&
                        (o.Status == OrderStatus.Weighing ||
                         o.Status == OrderStatus.WeightConfirmed ||
                         o.Status == OrderStatus.Washing ||
                         o.Status == OrderStatus.Drying ||
                         o.Status == OrderStatus.AwaitingPayment ||
                         o.Status == OrderStatus.PaymentConfirmed ||
                         o.Status == OrderStatus.ReadyForDelivery ||
                         o.Status == OrderStatus.Delivered ||
                         o.Status == OrderStatus.Cancelled ||
                         o.Status == OrderStatus.Abandoned))
            .OrderByDescending(o => o.UpdatedAt ?? o.CreatedAt)
            .Take(30)
            .ToListAsync();

        var model = new RiderDashboardViewModel
        {
            Rider = user,
            ActiveJobs = myJobs,
            JobHistory = history
        };

        ViewData["ActiveTab"] = tab ?? "dashboard";
        return View(model);
    }

    // POST /Rider/MarkPickedUp/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPickedUp(int id, IFormFile? photo)
    {
        var user = await _userManager.GetUserAsync(User);
        var order = await _context.LaundryOrders.FindAsync(id);

        if (order == null || order.PickupRiderId != user?.Id || order.Status != OrderStatus.RiderAssigned)
        {
            TempData["ErrorMessage"] = "Cannot mark this order as picked up.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        if (photo == null || photo.Length == 0)
        {
            TempData["ErrorMessage"] = "A pickup photo is required.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        const long maxPhotoBytes = 10 * 1024 * 1024;
        if (photo.Length > maxPhotoBytes)
        {
            TempData["ErrorMessage"] = "The pickup photo must be 10 MB or smaller.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedExtensions.Contains(extension) || !allowedContentTypes.Contains(photo.ContentType.ToLowerInvariant()))
        {
            TempData["ErrorMessage"] = "Please upload a JPG, PNG, or WEBP pickup photo.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        var dir = Path.Combine(_env.WebRootPath, "uploads", "orders", id.ToString());
        Directory.CreateDirectory(dir);
        var fileName = $"pickup{extension}";
        var filePath = Path.Combine(dir, fileName);
        using var stream = new FileStream(filePath, FileMode.Create);
        await photo.CopyToAsync(stream);
        order.PickupPhotoPath = $"/uploads/orders/{id}/{fileName}";

        order.Status = OrderStatus.PickedUp;
        order.PickedUpAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _notificationService.NotifyAsync(order.CustomerId, order.Id, LaundryHub2._0.Services.NotificationService.PickedUp, $"Order {order.OrderNumber} picked up", "Your laundry was picked up and is on its way to the shop.");
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Order {order.OrderNumber} marked as Picked Up!";
        return RedirectToAction(nameof(Index), new { tab = "jobs" });
    }

    // POST /Rider/MarkInTransit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkInTransit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var order = await _context.LaundryOrders.FindAsync(id);

        if (order == null || order.PickupRiderId != user?.Id || order.Status != OrderStatus.PickedUp)
        {
            TempData["ErrorMessage"] = "Cannot update this order status.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        order.Status = OrderStatus.InTransitToShop;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Order {order.OrderNumber} is now In Transit to Shop.";
        return RedirectToAction(nameof(Index), new { tab = "jobs" });
    }

    // POST /Rider/ArrivedAtShop/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArrivedAtShop(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var order = await _context.LaundryOrders.FindAsync(id);

        if (order == null || order.PickupRiderId != user?.Id ||
            (order.Status != OrderStatus.PickedUp && order.Status != OrderStatus.InTransitToShop))
        {
            TempData["ErrorMessage"] = "Cannot mark arrival for this order.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        order.Status = OrderStatus.Weighing;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Order {order.OrderNumber} arrived at shop — now Weighing.";
        return RedirectToAction(nameof(Index), new { tab = "jobs" });
    }

    // POST /Rider/MarkDelivered/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDelivered(int id, IFormFile? photo)
    {
        var user = await _userManager.GetUserAsync(User);
        var order = await _context.LaundryOrders.FindAsync(id);

        if (order == null || order.DeliveryRiderId != user?.Id || order.Status != OrderStatus.OutForDelivery)
        {
            TempData["ErrorMessage"] = "Cannot mark delivery for this order.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        if (photo == null || photo.Length == 0)
        {
            TempData["ErrorMessage"] = "A proof of delivery photo is required.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        const long maxPhotoBytes = 10 * 1024 * 1024;
        if (photo.Length > maxPhotoBytes)
        {
            TempData["ErrorMessage"] = "The delivery photo must be 10 MB or smaller.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedExtensions.Contains(extension) || !allowedContentTypes.Contains(photo.ContentType.ToLowerInvariant()))
        {
            TempData["ErrorMessage"] = "Please upload a JPG, PNG, or WEBP delivery photo.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        var dir = Path.Combine(_env.WebRootPath, "uploads", "orders", id.ToString());
        Directory.CreateDirectory(dir);
        var fileName = $"delivery{extension}";
        var filePath = Path.Combine(dir, fileName);
        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await photo.CopyToAsync(stream);
        }

        order.DeliveryPhotoPath = $"/uploads/orders/{id}/{fileName}";

        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await _notificationService.NotifyAsync(order.CustomerId, order.Id, LaundryHub2._0.Services.NotificationService.Delivered, $"Order {order.OrderNumber} delivered", "Your laundry has been delivered.");
        var paidAmount = (order.TotalAmount ?? 0m) + (order.AccruedPenaltyAmount ?? 0m);
        if (paidAmount > 0)
        {
            _context.LoyaltyTransactions.Add(new LoyaltyTransaction
            {
                CustomerId = order.CustomerId,
                OrderId = order.Id,
                Type = LoyaltyTransactionType.Earn,
                Points = (int)Math.Floor(paidAmount / 10m),
                Reason = $"Order {order.OrderNumber} delivery",
                ExpiresAt = DateTime.UtcNow.AddMonths(12),
                CreatedAt = DateTime.UtcNow
            });
        }
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            var fresh = await _context.LaundryOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == order.Id);
            if (fresh != null && fresh.Status == OrderStatus.Delivered)
            {
                TempData["SuccessMessage"] = $"Order {order.OrderNumber} successfully marked as Delivered! 🎉";
                return RedirectToAction(nameof(Index), new { tab = "jobs" });
            }
            TempData["ErrorMessage"] = "Another update conflicted with this delivery. Please check the order status and try again.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        TempData["SuccessMessage"] = $"Order {order.OrderNumber} successfully marked as Delivered! 🎉";
        return RedirectToAction(nameof(Index), new { tab = "jobs" });
    }

    // POST /Rider/MarkDeliveryAttemptFailed/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDeliveryAttemptFailed(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var order = await _context.LaundryOrders.FindAsync(id);

        if (order == null || order.DeliveryRiderId != user?.Id || order.Status != OrderStatus.OutForDelivery)
        {
            TempData["ErrorMessage"] = "Cannot log a failed attempt for this order.";
            return RedirectToAction(nameof(Index), new { tab = "jobs" });
        }

        order.DeliveryAttemptCount = (order.DeliveryAttemptCount ?? 0) + 1;
        order.Status = OrderStatus.DeliveryAttemptFailed;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Delivery attempt #{order.DeliveryAttemptCount} logged for order {order.OrderNumber}. Ask dispatch to reassign when ready.";
        return RedirectToAction(nameof(Index), new { tab = "jobs" });
    }
}
