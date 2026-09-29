namespace LaundryHub2._0.Models;

public static class ClaimType
{
    public const string Damage = "Damage";
    public const string Loss = "Loss";
    public const string Other = "Other";
}

public static class ClaimStatus
{
    public const string Open = "Open";
    public const string Resolved = "Resolved";
    public const string Rejected = "Rejected";
}

public class Claim
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public LaundryOrder? Order { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public bool ReporterIsStaff { get; set; }
    public string Type { get; set; } = ClaimType.Other;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = ClaimStatus.Open;
    public string? ResolutionNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
