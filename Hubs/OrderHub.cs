using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Data;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Hubs;

/// <summary>
/// Hub for real-time order lifecycle events and live GPS tracking broadcasts.
/// Connection to order groups is strictly authorized on the server side.
/// </summary>
[Authorize]
public class OrderHub : Hub
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrderHub(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// Join the SignalR group for an individual order to receive real-time updates and GPS telemetry.
    /// Only the customer who owns the order, assigned pickup/delivery riders, or staff/managers/admins can join.
    /// </summary>
    public async Task<bool> JoinOrderGroup(int orderId)
    {
        if (Context.User == null) return false;
        var user = await _userManager.GetUserAsync(Context.User);
        if (user == null) return false;

        var order = await _context.LaundryOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null) return false;

        var isOwner = order.CustomerId == user.Id;
        var isAssignedRider = order.PickupRiderId == user.Id || order.DeliveryRiderId == user.Id;
        var isStaffOrAdmin = Context.User?.IsInRole("Admin") == true ||
                             Context.User?.IsInRole("Manager") == true ||
                             Context.User?.IsInRole("Staff") == true;

        if (isOwner || isAssignedRider || isStaffOrAdmin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderId}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Leave an order's SignalR group.
    /// </summary>
    public async Task LeaveOrderGroup(int orderId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order-{orderId}");
    }

    /// <summary>
    /// Join the admin dispatch monitoring group.
    /// Strictly restricted to Admin and Manager roles.
    /// </summary>
    public async Task<bool> JoinDispatchGroup()
    {
        var isAdminOrManager = Context.User?.IsInRole("Admin") == true ||
                               Context.User?.IsInRole("Manager") == true;

        if (isAdminOrManager)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admin-dispatch");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Leave the admin dispatch monitoring group.
    /// </summary>
    public async Task LeaveDispatchGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admin-dispatch");
    }
}
