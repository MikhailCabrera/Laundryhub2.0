namespace LaundryHub2._0.Models;

public class AuditLog
{
    public int Id { get; set; }

    /// <summary>
    /// The admin who performed the action.
    /// </summary>
    public string AdminName { get; set; } = string.Empty;

    /// <summary>
    /// Action performed: "Add" | "Update" | "Archive" | "Suspend" | "Unsuspend"
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Full name of the user affected.
    /// </summary>
    public string TargetUser { get; set; } = string.Empty;

    /// <summary>
    /// Role of the user affected.
    /// </summary>
    public string TargetRole { get; set; } = string.Empty;

    /// <summary>
    /// Additional notes (e.g. suspension reason).
    /// </summary>
    public string? Notes { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
