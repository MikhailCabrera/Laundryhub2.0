using LaundryHub2._0.Data;
using LaundryHub2._0.Models;

namespace LaundryHub2._0.Services;

public class DatabaseNotificationSender : INotificationSender
{
    private readonly ApplicationDbContext _context;

    public DatabaseNotificationSender(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task SendAsync(string recipientUserId, int orderId, string type, string title, string body)
    {
        _context.Notifications.Add(new Notification
        {
            RecipientUserId = recipientUserId,
            OrderId = orderId,
            Type = type,
            Title = title,
            Body = body,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
        return Task.CompletedTask;
    }
}
