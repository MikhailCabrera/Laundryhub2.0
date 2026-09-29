using System.ComponentModel.DataAnnotations;

namespace LaundryHub2._0.Models;

public class CustomerDashboardViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public List<LaundryService> AvailableServices { get; set; } = new();
    public List<LaundryOrder> Orders { get; set; } = new();

    public int ActiveOrdersCount => Orders.Count(o => 
        o.Status != OrderStatus.Delivered && 
        o.Status != OrderStatus.Cancelled && 
        o.Status != OrderStatus.Abandoned);

    public int PendingPickupsCount => Orders.Count(o => o.Status == OrderStatus.Pending);

    public List<Notification> Notifications { get; set; } = new();

    public int UnreadNotificationsCount => Notifications.Count(n => !n.IsRead);

    public int LoyaltyBalance { get; set; }

    public List<LoyaltyTransaction> LoyaltyLedger { get; set; } = new();

    public Dictionary<int, decimal> LoyaltyDiscounts { get; set; } = new();

    public int CompletedOrdersCount => Orders.Count(o => o.Status == OrderStatus.Delivered);

    /// <summary>Net cash spent: base + late penalty − redeemed loyalty discounts, floored at zero per order.</summary>
    public decimal TotalSpent => Orders
        .Where(o => o.IsPaymentConfirmed && o.TotalAmount.HasValue)
        .Sum(o => Math.Max(0m, o.TotalAmount!.Value + (o.AccruedPenaltyAmount ?? 0m) - (LoyaltyDiscounts.TryGetValue(o.Id, out var d) ? d : 0m)));

    /// <summary>Net amount still due across unpaid orders (base + penalty − discounts).</summary>
    public decimal OutstandingBalance => Orders
        .Where(o => !o.IsPaymentConfirmed && o.TotalAmount.HasValue && o.Status != OrderStatus.Cancelled && !o.IsAbandoned)
        .Sum(o => Math.Max(0m, (o.TotalAmount ?? 0m) + (o.AccruedPenaltyAmount ?? 0m) - (LoyaltyDiscounts.TryGetValue(o.Id, out var d) ? d : 0m)));
}

public class BookPickupInputModel
{
    [Required(ErrorMessage = "Please select at least one laundry service.")]
    public List<int> SelectedServiceIds { get; set; } = new();

    [Required(ErrorMessage = "Preferred pickup date is required.")]
    [DataType(DataType.Date)]
    public DateTime PreferredPickupDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Preferred pickup time slot is required.")]
    public string PreferredPickupTime { get; set; } = "8:00 AM - 10:00 AM";

    [Required(ErrorMessage = "Pickup address / location is required.")]
    [StringLength(500, ErrorMessage = "Location cannot exceed 500 characters.")]
    public string PickupLocation { get; set; } = string.Empty;

    public decimal? PickupLatitude { get; set; }
    public decimal? PickupLongitude { get; set; }

    [Required(ErrorMessage = "Contact number is required.")]
    [RegularExpression(@"^(09|\+639)\d{9}$", ErrorMessage = "Please enter a valid Philippine mobile number.")]
    public string ContactNumber { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Special instructions cannot exceed 1000 characters.")]
    public string? SpecialInstructions { get; set; }

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the Terms & Conditions.")]
    public bool AcceptTerms { get; set; }
}

public class RedeemPointsInputModel
{
    [Required(ErrorMessage = "Order is required.")]
    public int OrderId { get; set; }

    [Required(ErrorMessage = "Points are required.")]
    [Range(100, 1000000, ErrorMessage = "Points must be between 100 and 1,000,000.")]
    public int Points { get; set; }
}

/// <summary>
/// Read-only payment receipt projection. Built only from already-persisted
/// <see cref="LaundryOrder"/> fields plus the redeemed loyalty discount —
/// no new table, no card data, no secrets.
/// </summary>
public class PaymentReceiptViewModel
{
    public LaundryOrder Order { get; set; } = null!;
    public string CustomerName { get; set; } = string.Empty;
    public decimal BaseAmount { get; set; }
    public decimal PenaltyAmount { get; set; }
    public decimal LoyaltyDiscount { get; set; }
    public decimal NetPaid { get; set; }
}
