namespace LaundryHub2._0.Models;

public class RiderDashboardViewModel
{
    public ApplicationUser Rider { get; set; } = null!;

    /// <summary>Orders currently assigned/active for this rider (pickup leg)</summary>
    public List<LaundryOrder> ActiveJobs { get; set; } = new();

    /// <summary>Past pickup jobs (arrived at shop or beyond)</summary>
    public List<LaundryOrder> JobHistory { get; set; } = new();

    public List<Notification> Notifications { get; set; } = new();

    public int UnreadNotificationsCount => Notifications.Count(n => !n.IsRead);

    public int TotalPickupsCompleted => JobHistory.Count(o =>
        o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Abandoned);
}
