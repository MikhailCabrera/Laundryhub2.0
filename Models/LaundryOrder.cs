namespace LaundryHub2._0.Models;

public class LaundryOrder
{
    public const string CurrentTermsVersion = "1.0";

    public int Id { get; set; }

    /// <summary>Human-readable order number, e.g. LH-20260924-0001</summary>
    public string OrderNumber { get; set; } = string.Empty;

    // ── Customer ──────────────────────────────────────────────────────────────
    public string CustomerId { get; set; } = string.Empty;
    public ApplicationUser Customer { get; set; } = null!;

    // ── Selected services (join table) ────────────────────────────────────────
    public ICollection<LaundryOrderService> OrderServices { get; set; } = new List<LaundryOrderService>();

    // ── Pickup details ────────────────────────────────────────────────────────
    public DateTime PreferredPickupDate { get; set; }
    public string PreferredPickupTime { get; set; } = string.Empty;   // e.g. "10:00 AM"
    public string PickupLocation { get; set; } = string.Empty;
    public decimal? PickupLatitude { get; set; }
    public decimal? PickupLongitude { get; set; }
    public string ContactNumber { get; set; } = string.Empty;
    public string? SpecialInstructions { get; set; }

    // ── Status ────────────────────────────────────────────────────────────────
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public OrderOrigin Origin { get; set; } = OrderOrigin.Pickup;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime RowVersion { get; set; }

    // ── Pickup leg rider ─────────────────────────────────────────────────────
    public string? PickupRiderId { get; set; }
    public ApplicationUser? PickupRider { get; set; }
    public DateTime? RiderAssignedAt { get; set; }

    // ── Pickup evidence ───────────────────────────────────────────────────────
    /// <summary>Relative path under wwwroot/uploads/orders/{id}/pickup.*</summary>
    public string? PickupPhotoPath { get; set; }
    public DateTime? PickedUpAt { get; set; }

    // ── Weighing ──────────────────────────────────────────────────────────────
    public decimal? WeightKg { get; set; }
    public DateTime? WeightConfirmedByCustomerAt { get; set; }
    /// <summary>Relative path under wwwroot/uploads/orders/{id}/weight.*</summary>
    public string? WeightPhotoPath { get; set; }
    public DateTime? WeightConfirmedAt { get; set; }
    public DateTime? WeightConfirmationDeadline { get; set; }
    public DateTime? WeightConfirmationExtensionDeadline { get; set; }
    public string? WeightOverrideByStaffId { get; set; }
    public string? WeightOverrideSupervisorId { get; set; }
    public DateTime? WeightOverrideApprovedAt { get; set; }
    public DateTime? WeightOverrideAt { get; set; }
    public string? WeightOverrideReason { get; set; }
    public string? EstimatedWeightMethod { get; set; }
    /// <summary>Calculated: WeightKg × blended service rate. Stored once confirmed.</summary>
    public decimal? TotalAmount { get; set; }

    // ── Service processing stages ─────────────────────────────────────────────
    public DateTime? WashingStartedAt { get; set; }
    public DateTime? DryingStartedAt { get; set; }
    public DateTime? ProcessingCompletedAt { get; set; }

    // ── Payment ───────────────────────────────────────────────────────────────
    public bool IsPaymentConfirmed { get; set; } = false;
    public string? PayMongoPaymentId { get; set; }
    public string? PayMongoCheckoutUrl { get; set; }
    public DateTime? PaymentConfirmedAt { get; set; }
    public decimal? RefundAmount { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? PayMongoWebhookEventId { get; set; }
    public DateTime? PayMongoWebhookReceivedAt { get; set; }

    // ── Payment deadline and late penalty ─────────────────────────────────────
    /// <summary>
    /// The moment the customer was notified that their order is ReadyForDelivery.
    /// Kept for informational/display purposes; the authoritative payment deadline
    /// clock is now driven by <see cref="PaymentDeadlineAt"/>.
    /// </summary>
    public DateTime? ReadyForDeliveryNotifiedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the order entered AwaitingPayment — the moment the
    /// customer is formally asked to pay. Set once and never overwritten.
    /// </summary>
    public DateTime? AwaitingPaymentAt { get; set; }

    /// <summary>
    /// The original 24-hour payment deadline: AwaitingPaymentAt + 24 hours.
    /// This is the immutable anchor for all downstream deadline calculations.
    /// Set once when AwaitingPaymentAt is first written; never reset.
    /// </summary>
    public DateTime? PaymentDeadlineAt { get; set; }

    /// <summary>
    /// End of the 72-hour grace period: PaymentDeadlineAt + 72 hours.
    /// Late-payment penalty accrual begins only after this timestamp.
    /// Set once alongside PaymentDeadlineAt; never reset.
    /// </summary>
    public DateTime? GracePeriodEndAt { get; set; }

    /// <summary>
    /// Accrued late-payment penalty: 3% of TotalAmount per day overdue, capped at 30% (10 days).
    /// Overdue days are measured from GracePeriodEndAt, not from PaymentDeadlineAt.
    /// Re-calculated and stored on read or via background job.
    /// </summary>
    public decimal? AccruedPenaltyAmount { get; set; }

    public bool IsAbandoned { get; set; } = false;
    public DateTime? AbandonedAt { get; set; }

    // ── Delivery leg rider ────────────────────────────────────────────────────
    public string? DeliveryRiderId { get; set; }
    public ApplicationUser? DeliveryRider { get; set; }
    public DateTime? DeliveryAssignedAt { get; set; }

    // ── Delivery evidence ─────────────────────────────────────────────────────
    /// <summary>Relative path under wwwroot/uploads/orders/{id}/delivery.*</summary>
    public string? DeliveryPhotoPath { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public int? DeliveryAttemptCount { get; set; }

    // ── Terms & Conditions acceptance ─────────────────────────────────────────
    public DateTime? TermsAcceptedAt { get; set; }
    /// <summary>Version of the T&amp;C the customer accepted, e.g. "1.0"</summary>
    public string TermsVersion { get; set; } = CurrentTermsVersion;
}
