using LaundryHub2._0.Data;
using LaundryHub2._0.Models;
using Microsoft.EntityFrameworkCore;

namespace LaundryHub2._0.Services;

public class LoyaltyService
{
    private readonly ApplicationDbContext _context;

    public LoyaltyService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetBalanceAsync(string customerId)
    {
        var now = DateTime.UtcNow;
        var balance = await _context.LoyaltyTransactions
            .Where(t => t.CustomerId == customerId
                && !(t.Type == LoyaltyTransactionType.Earn && t.ExpiresAt != null && t.ExpiresAt < now))
            .SumAsync(t => (int?)t.Points);
        return balance ?? 0;
    }

    public async Task<decimal> GetRedeemedDiscountAsync(int orderId, decimal orderTotal)
    {
        var points = await _context.LoyaltyTransactions
            .Where(t => t.OrderId == orderId && t.Type == LoyaltyTransactionType.Redeem)
            .SumAsync(t => (int?)t.Points) ?? 0;
        if (points >= 0)
            return 0m;
        return Math.Min(-(decimal)points / 100m * 10m, orderTotal);
    }

    public async Task<decimal> ComputeAmountDueAsync(LaundryOrder order)
    {
        var baseAmount = (order.TotalAmount ?? 0m) + (order.AccruedPenaltyAmount ?? 0m);
        var discount = await GetRedeemedDiscountAsync(order.Id, order.TotalAmount ?? 0m);
        return Math.Max(0m, baseAmount - discount);
    }
}
