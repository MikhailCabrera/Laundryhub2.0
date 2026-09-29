using Microsoft.AspNetCore.Identity;

namespace LaundryHub2._0.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? District { get; set; }
    public bool IsSuspended { get; set; } = false;
    public string? SuspendNote { get; set; }
    public bool IsArchived { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Rider Location Tracking (Phase 2A) ───────────────────────────────────
    public decimal? CurrentLatitude { get; set; }
    public decimal? CurrentLongitude { get; set; }
    public DateTime? LastLocationUpdatedAt { get; set; }
}
