using System.ComponentModel.DataAnnotations;

namespace LaundryHub2._0.Models;

public class AdminDashboardViewModel
{
    public ApplicationUser CurrentUser { get; set; } = null!;
    public List<LaundryOrder> Orders { get; set; } = new();
    public List<ApplicationUser> AvailableRiders { get; set; } = new();
    public List<InventoryItem> InventoryItems { get; set; } = new();
    public List<InventoryTransaction> InventoryTransactions { get; set; } = new();
    public List<LaundryService> AvailableServices { get; set; } = new();
    public List<AuditLog> AuditEntries { get; set; } = new();
    public List<CustomerNote> CustomerNotes { get; set; } = new();
    public List<LoyaltyTransaction> LoyaltyTransactions { get; set; } = new();
    public List<Claim> Claims { get; set; } = new();

    public int PendingOrdersCount => Orders.Count(o => o.Status == OrderStatus.Pending);

    public int OngoingOrdersCount => Orders.Count(o => 
        o.Status is OrderStatus.RiderAssigned 
                 or OrderStatus.PickedUp 
                 or OrderStatus.InTransitToShop 
                 or OrderStatus.Weighing 
                 or OrderStatus.WeightConfirmed 
                 or OrderStatus.Washing 
                 or OrderStatus.Drying
                 or OrderStatus.PaymentConfirmed
                 or OrderStatus.ReadyForDelivery
                 or OrderStatus.OutForDelivery);

    public int AwaitingPaymentCount => Orders.Count(o => o.Status == OrderStatus.AwaitingPayment);

    public int CompletedOrdersCount => Orders.Count(o => o.Status == OrderStatus.Delivered);

    public int CancelledOrdersCount => Orders.Count(o => o.Status == OrderStatus.Cancelled);

    public int TotalCustomersCount { get; set; }
    public int TotalEmployeesCount { get; set; }

    /// <summary>
    /// Real Identity users for the Employee Management table
    /// (roles Admin, Manager, Staff, Rider), newest first.
    /// A flattened DTO is used deliberately so password hashes, security
    /// stamps and other Identity internals are never serialized to the browser.
    /// </summary>
    public List<AdminUserRowViewModel> Employees { get; set; } = new();

    /// <summary>
    /// Real Identity users in the Customer role, newest first.
    /// </summary>
    public List<AdminUserRowViewModel> Customers { get; set; } = new();
}

/// <summary>
/// Flattened, browser-safe projection of an ApplicationUser plus its role.
/// </summary>
public class AdminUserRowViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? District { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsSuspended { get; set; }
    public string? SuspendNote { get; set; }
    public bool IsArchived { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class SuspendUserInputModel
{
    [Required(ErrorMessage = "User ID is required.")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "A suspension reason is required.")]
    [MaxLength(500, ErrorMessage = "Suspension reason is too long (max 500 characters).")]
    public string SuspendNote { get; set; } = string.Empty;
}

public class UnsuspendUserInputModel
{
    [Required(ErrorMessage = "User ID is required.")]
    public string UserId { get; set; } = string.Empty;
}

public class CreateUserInputModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be 2-100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Suspend note is too long (max 500 characters).")]
    public string? SuspendNote { get; set; }
}

public class EditUserInputModel
{
    [Required(ErrorMessage = "User ID is required.")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be 2-100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; } = string.Empty;
}

public class SetUserArchivedInputModel
{
    [Required(ErrorMessage = "User ID is required.")]
    public string UserId { get; set; } = string.Empty;

    public bool IsArchived { get; set; }
}

public class AssignRiderInputModel
{
    [Required(ErrorMessage = "Order ID is required.")]
    public int OrderId { get; set; }

    [Required(ErrorMessage = "Please select a rider.")]
    public string RiderId { get; set; } = string.Empty;
}

public class ReassignRiderInputModel
{
    [Required(ErrorMessage = "Order ID is required.")]
    public int OrderId { get; set; }

    [Required(ErrorMessage = "Please select a rider.")]
    public string RiderId { get; set; } = string.Empty;
}

public class CreateWalkInOrderInputModel
{
    [Required(ErrorMessage = "Customer is required.")]
    public string CustomerId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select at least one laundry service.")]
    public List<int> ServiceIds { get; set; } = new();

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the Terms & Conditions.")]
    public bool AcceptTerms { get; set; }
}

public class AddCustomerNoteInputModel
{
    [Required(ErrorMessage = "Customer is required.")]
    public string CustomerId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Note text is required.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Note text must be 1-1000 characters.")]
    public string Text { get; set; } = string.Empty;
}

public class AdjustLoyaltyPointsInputModel
{
    [Required(ErrorMessage = "Customer is required.")]
    public string CustomerId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Points are required.")]
    [Range(-1000000, 1000000, ErrorMessage = "Points must be between -1,000,000 and 1,000,000.")]
    public int Points { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "Reason must be 2-500 characters.")]
    public string Reason { get; set; } = string.Empty;
}

public class RefundOrderInputModel
{
    [Required(ErrorMessage = "Order ID is required.")]
    public int OrderId { get; set; }
}

public class FileClaimInputModel
{
    [Required(ErrorMessage = "Order number is required.")]
    public string OrderNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Claim type is required.")]
    public string Type { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Description must be 1-1000 characters.")]
    public string Description { get; set; } = string.Empty;
}

public class ResolveClaimInputModel
{
    [Required(ErrorMessage = "Claim ID is required.")]
    public int ClaimId { get; set; }

    [Required(ErrorMessage = "Resolution status is required.")]
    public string Status { get; set; } = string.Empty;

    [Required(ErrorMessage = "Resolution note is required.")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Resolution note must be 1-500 characters.")]
    public string ResolutionNote { get; set; } = string.Empty;
}
