namespace LaundryHub2._0.Models;

public class AppUser
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Values: "Admin" | "Manager" | "Staff" | "Rider" | "Customer"
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// True when an admin has suspended this account.
    /// </summary>
    public bool IsSuspended { get; set; } = false;

    /// <summary>
    /// Admin-provided reason for suspension.
    /// </summary>
    public string? SuspendNote { get; set; }

    /// <summary>
    /// True when the account has been archived (soft-delete).
    /// </summary>
    public bool IsArchived { get; set; } = false;
}
