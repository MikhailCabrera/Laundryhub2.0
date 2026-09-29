using Microsoft.EntityFrameworkCore;
using LaundryHub2._0.Data;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Services;

public class PaymentDeadlineBackfillResult
{
    public int TotalAwaitingPaymentInspected { get; set; }
    public int AlreadySetCount { get; set; }
    public int SuccessfullyBackfilledCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Details { get; set; } = new();
    public List<string> ExcludedOrdersReport { get; set; } = new();
}

public class PaymentDeadlineBackfillService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PaymentDeadlineBackfillService> _logger;

    public PaymentDeadlineBackfillService(
        ApplicationDbContext context,
        ILogger<PaymentDeadlineBackfillService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Executes a safe, idempotent backfill on orders currently in AwaitingPayment
    /// that are missing AwaitingPaymentAt.
    /// </summary>
    public async Task<PaymentDeadlineBackfillResult> ExecuteBackfillAsync(string executedBy = "System Administrator")
    {
        var result = new PaymentDeadlineBackfillResult();

        // 1. Strict scope: ONLY orders currently in AwaitingPayment
        var orders = await _context.LaundryOrders
            .Where(o => o.Status == OrderStatus.AwaitingPayment)
            .ToListAsync();

        result.TotalAwaitingPaymentInspected = orders.Count;

        if (orders.Count == 0)
        {
            result.Details.Add("No orders currently in AwaitingPayment found.");
            return result;
        }

        // Candidate orders: orders in AwaitingPayment missing any of the 3 fields
        var candidateOrders = orders
            .Where(o => !o.AwaitingPaymentAt.HasValue || !o.PaymentDeadlineAt.HasValue || !o.GracePeriodEndAt.HasValue)
            .ToList();
        result.AlreadySetCount = orders.Count - candidateOrders.Count;

        if (candidateOrders.Count == 0)
        {
            result.Details.Add($"All {orders.Count} AwaitingPayment order(s) already have all three payment timeline fields set. No changes needed.");
            return result;
        }

        // Cache candidate order numbers for audit log searching
        var candidateOrderNumbers = candidateOrders.Select(o => o.OrderNumber).ToList();

        // Query candidate AuditLogs that could match weight confirmation or override
        var auditLogs = await _context.AuditLogs
            .Where(a => candidateOrderNumbers.Any(num => a.Notes != null && a.Notes.Contains(num)))
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        foreach (var order in candidateOrders)
        {
            // Idempotent safety check: if all three are already populated, nothing to do
            if (order.AwaitingPaymentAt.HasValue && order.PaymentDeadlineAt.HasValue && order.GracePeriodEndAt.HasValue)
            {
                result.AlreadySetCount++;
                continue;
            }

            DateTime? anchorTimestamp = order.AwaitingPaymentAt;
            string? source = null;

            if (anchorTimestamp.HasValue)
            {
                source = "Existing AwaitingPaymentAt";
            }
            // Priority 1: Customer confirmation
            else if (order.WeightConfirmedByCustomerAt.HasValue)
            {
                anchorTimestamp = order.WeightConfirmedByCustomerAt.Value;
                source = "CustomerConfirmation (WeightConfirmedByCustomerAt)";
            }
            // Priority 2: Staff/supervisor estimated-weight override
            else if (order.WeightOverrideApprovedAt.HasValue)
            {
                anchorTimestamp = order.WeightOverrideApprovedAt.Value;
                source = "StaffWeightOverride (WeightOverrideApprovedAt)";
            }
            else if (order.WeightOverrideAt.HasValue)
            {
                anchorTimestamp = order.WeightOverrideAt.Value;
                source = "StaffWeightOverride (WeightOverrideAt)";
            }
            // Priority 3: AuditTrail fallback
            else
            {
                var matchingAudit = auditLogs
                    .Where(a => a.Notes != null && a.Notes.Contains(order.OrderNumber))
                    .Where(a => (a.Action == AuditTrail.Update && a.Notes!.Contains("weight confirmed by customer"))
                             || (a.Action == "WeightOverride" && a.Notes!.Contains("estimated-weight override")))
                    .OrderByDescending(a => a.Timestamp)
                    .FirstOrDefault();

                if (matchingAudit != null)
                {
                    anchorTimestamp = matchingAudit.Timestamp;
                    source = $"AuditTrail ({matchingAudit.Action} at {matchingAudit.Timestamp:yyyy-MM-dd HH:mm:ss} UTC)";
                }
            }

            // Priority 4: No reliable anchor -> leave untouched and report
            if (!anchorTimestamp.HasValue || string.IsNullOrEmpty(source))
            {
                result.SkippedCount++;
                var reason = "No reliable historical anchor found (AwaitingPaymentAt, WeightConfirmedByCustomerAt, WeightOverrideApprovedAt, and unambiguous AuditTrail are all missing).";
                result.ExcludedOrdersReport.Add($"Order #{order.OrderNumber} (ID: {order.Id}): EXCLUDED - {reason}");
                _logger.LogWarning("Order {OrderNumber} in AwaitingPayment skipped from backfill: {Reason}", order.OrderNumber, reason);
                continue;
            }

            var awaitingPaymentAt = anchorTimestamp.Value;
            var paymentDeadlineAt = awaitingPaymentAt.AddHours(24);
            var gracePeriodEndAt = awaitingPaymentAt.AddHours(24 + 72); // 96 hours

            var modifiedFields = new List<string>();

            // Never overwrite existing non-null values
            if (!order.AwaitingPaymentAt.HasValue)
            {
                order.AwaitingPaymentAt = awaitingPaymentAt;
                modifiedFields.Add("AwaitingPaymentAt");
            }

            if (!order.PaymentDeadlineAt.HasValue)
            {
                order.PaymentDeadlineAt = paymentDeadlineAt;
                modifiedFields.Add("PaymentDeadlineAt");
            }

            if (!order.GracePeriodEndAt.HasValue)
            {
                order.GracePeriodEndAt = gracePeriodEndAt;
                modifiedFields.Add("GracePeriodEndAt");
            }

            if (modifiedFields.Count == 0)
            {
                result.AlreadySetCount++;
                continue;
            }

            // Note: Do NOT change IsPaymentConfirmed, Status, WeightKg, TotalAmount, etc.
            result.SuccessfullyBackfilledCount++;

            var auditNote = $"Order {order.OrderNumber} (ID: {order.Id}) backfilled [{string.Join(", ", modifiedFields)}]. Source: {source}. Anchor: {awaitingPaymentAt:yyyy-MM-dd HH:mm:ss} UTC. PaymentDeadlineAt: {order.PaymentDeadlineAt:yyyy-MM-dd HH:mm:ss} UTC. GracePeriodEndAt: {order.GracePeriodEndAt:yyyy-MM-dd HH:mm:ss} UTC.";
            result.Details.Add(auditNote);

            AuditTrail.Record(
                _context,
                executedBy,
                "PaymentDeadlineBackfill",
                order.OrderNumber,
                "Order",
                auditNote);
        }

        if (result.SuccessfullyBackfilledCount > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Payment deadline backfill successfully committed {Count} order(s).", result.SuccessfullyBackfilledCount);
        }

        return result;
    }
}
