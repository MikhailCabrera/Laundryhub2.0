namespace LaundryHub2._0.Services;

public interface INotificationSender
{
    Task SendAsync(string recipientUserId, int orderId, string type, string title, string body);
}
