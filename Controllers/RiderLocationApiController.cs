using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Data;
using LaundryHub2._0.Hubs;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Controllers;

[ApiController]
[Route("api/rider")]
[Authorize(Roles = "Rider")]
[EnableRateLimiting("rider-gps-policy")]
public class RiderLocationApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly ILogger<RiderLocationApiController> _logger;

    public RiderLocationApiController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IHubContext<OrderHub> hubContext,
        ILogger<RiderLocationApiController> logger)
    {
        _context = context;
        _userManager = userManager;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Ingests authenticated rider GPS updates and broadcasts them to authorized SignalR groups.
    /// </summary>
    [HttpPost("location")]
    public async Task<IActionResult> UpdateLocation([FromBody] RiderLocationDto dto)
    {
        if (dto == null)
        {
            return BadRequest(new { success = false, message = "Location payload is required." });
        }

        // 1. Validate GPS Coordinates strictly
        if (dto.Latitude < -90m || dto.Latitude > 90m || dto.Longitude < -180m || dto.Longitude > 180m)
        {
            return BadRequest(new { success = false, message = "Invalid latitude or longitude coordinates." });
        }

        // 2. Identify rider ONLY from authenticated claims
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { success = false, message = "Authentication required." });
        }

        // 3. Active-Order Authorization: Rider must be assigned to an active tracking order.
        // Active pickup tracking: RiderAssigned, PickedUp, InTransitToShop (ends at Weighing/shop)
        // Active delivery tracking: ReadyForDelivery, OutForDelivery, DeliveryAttemptFailed (ends at Delivered)
        var activeOrdersQuery = _context.LaundryOrders
            .Where(o => (o.PickupRiderId == user.Id &&
                         (o.Status == OrderStatus.RiderAssigned ||
                          o.Status == OrderStatus.PickedUp ||
                          o.Status == OrderStatus.InTransitToShop)) ||
                        (o.DeliveryRiderId == user.Id &&
                         (o.Status == OrderStatus.ReadyForDelivery ||
                          o.Status == OrderStatus.OutForDelivery ||
                          o.Status == OrderStatus.DeliveryAttemptFailed)));

        LaundryOrder? activeOrder;

        if (dto.OrderId.HasValue && dto.OrderId.Value > 0)
        {
            activeOrder = await activeOrdersQuery.FirstOrDefaultAsync(o => o.Id == dto.OrderId.Value);
            if (activeOrder == null)
            {
                return BadRequest(new { success = false, message = "Specified order is not active or not assigned to the authenticated rider." });
            }
        }
        else
        {
            activeOrder = await activeOrdersQuery
                .OrderByDescending(o => o.DeliveryAssignedAt ?? o.RiderAssignedAt ?? o.UpdatedAt ?? o.CreatedAt)
                .FirstOrDefaultAsync();

            if (activeOrder == null)
            {
                return BadRequest(new { success = false, message = "No active tracking order currently assigned to this rider." });
            }
        }

        // 4. Server UTC timestamp is authoritative
        var nowUtc = DateTime.UtcNow;

        // 5. Update ApplicationUser rider coordinates using optimized ExecuteUpdateAsync
        // Avoids loading/overwriting unrelated properties and avoids optimistic concurrency conflicts.
        await _context.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.CurrentLatitude, dto.Latitude)
                .SetProperty(u => u.CurrentLongitude, dto.Longitude)
                .SetProperty(u => u.LastLocationUpdatedAt, nowUtc));

        // 6. Broadcast validated minimal payload to authorized SignalR groups
        var broadcastPayload = new RiderLocationBroadcastPayload
        {
            OrderId = activeOrder.Id,
            RiderId = user.Id,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            UpdatedAt = nowUtc
        };

        // Broadcast to customer/rider group for this specific order
        await _hubContext.Clients.Group($"order-{activeOrder.Id}")
            .SendAsync("RiderLocationUpdated", broadcastPayload);

        // Broadcast to admin dispatch group
        await _hubContext.Clients.Group("admin-dispatch")
            .SendAsync("RiderLocationUpdated", broadcastPayload);

        return Ok(new { success = true });
    }
}
