using LaundryHub2._0.Data;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Services;

public static class AuditTrail
{
    public const string Add = "Add";
    public const string Update = "Update";
    public const string Archive = "Archive";
    public const string Restore = "Restore";
    public const string Suspend = "Suspend";
    public const string Unsuspend = "Unsuspend";
    public const string Assign = "Assign";
    public const string Reassign = "Reassign";
    public const string Payment = "Payment";
    public const string Refund = "Refund";
    public const string Claim = "Claim";
    public const string Adjust = "Adjust";

    public static void Record(
        ApplicationDbContext context,
        string adminName,
        string action,
        string targetUser,
        string targetRole,
        string? notes)
    {
        context.AuditLogs.Add(new AuditLog
        {
            AdminName = adminName,
            Action = action,
            TargetUser = targetUser,
            TargetRole = targetRole,
            Notes = notes,
            Timestamp = DateTime.UtcNow
        });
    }
}
