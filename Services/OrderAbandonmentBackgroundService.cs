using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Data;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Services;

/// <summary>
/// Hosted background service that routinely sweeps unpaid orders to:
/// 1. Recalculate late-payment penalties past the 72-hour grace period.
/// 2. Mark orders abandoned when now >= PaymentDeadlineAt + 15 days (measured from the ORIGINAL 24h deadline).
/// </summary>
public class OrderAbandonmentBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderAbandonmentBackgroundService> _logger;
    private readonly TimeSpan _period = TimeSpan.FromMinutes(15);

    public OrderAbandonmentBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OrderAbandonmentBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderAbandonmentBackgroundService started.");

        using var timer = new PeriodicTimer(_period);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOrdersAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred executing order abandonment & penalty sweep.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("OrderAbandonmentBackgroundService stopped.");
    }

    private async Task ProcessPendingOrdersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;

        // Query unpaid, non-abandoned orders that have a payment deadline anchor set.
        var orders = await context.LaundryOrders
            .Where(o => !o.IsPaymentConfirmed 
                     && !o.IsAbandoned 
                     && o.PaymentDeadlineAt.HasValue 
                     && o.TotalAmount.HasValue)
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
            return;

        var modifiedCount = 0;
        var abandonedCount = 0;

        foreach (var order in orders)
        {
            var deadline = order.PaymentDeadlineAt!.Value;
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
                    order.AccruedPenaltyAmount = Math.Round(order.TotalAmount!.Value * 0.30m, 2); // maximum cap
                    modifiedCount++;
                    abandonedCount++;

                    AuditTrail.Record(context, "System", AuditTrail.Update, order.OrderNumber, "Order",
                        $"Order {order.OrderNumber} marked as Abandoned: 15 days elapsed past original payment deadline.");
                }
                else if (now > gracePeriodEnd)
                {
                    // Late penalty after 72-hour grace period: 3% of TotalAmount per day overdue, capped at 30%.
                    var overdueSpan = now - gracePeriodEnd;
                    var daysOverdue = (decimal)overdueSpan.TotalDays;
                    var rawPenalty = daysOverdue * 0.03m * order.TotalAmount!.Value;
                    var maxPenalty = order.TotalAmount.Value * 0.30m;
                    var newPenalty = Math.Round(Math.Min(rawPenalty, maxPenalty), 2);

                    if (order.AccruedPenaltyAmount != newPenalty)
                    {
                        order.AccruedPenaltyAmount = newPenalty;
                        order.UpdatedAt = now;
                        modifiedCount++;
                    }
                }
                else
                {
                    // Inside 72-hour grace period: no penalty
                    if (order.AccruedPenaltyAmount != 0m)
                    {
                        order.AccruedPenaltyAmount = 0m;
                        order.UpdatedAt = now;
                        modifiedCount++;
                    }
                }
            }
            else
            {
                // Prior to deadline: zero penalty
                if (order.AccruedPenaltyAmount != 0m)
                {
                    order.AccruedPenaltyAmount = 0m;
                    order.UpdatedAt = now;
                    modifiedCount++;
                }
            }
        }

        if (modifiedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Processed {ModifiedCount} orders in abandonment/penalty sweep ({AbandonedCount} newly abandoned).",
                modifiedCount, abandonedCount);
        }
    }
}
