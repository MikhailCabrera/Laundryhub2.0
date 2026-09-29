namespace LaundryHub2._0.Services;

public class NotificationService
{
    public const string PickedUp = "PickedUp";
    public const string WeightConfirmed = "WeightConfirmed";
    public const string ReadyForDelivery = "ReadyForDelivery";
    public const string OutForDelivery = "OutForDelivery";
    public const string Delivered = "Delivered";
    public const string PaymentConfirmed = "PaymentConfirmed";
    public const string RefundIssued = "RefundIssued";
    public const string WeightConfirmRequest = "WeightConfirmRequest";

    private readonly INotificationSender _sender;

    public NotificationService(INotificationSender sender)
    {
        _sender = sender;
    }

    public Task NotifyAsync(string recipientUserId, int orderId, string type, string title, string body)
    {
        return _sender.SendAsync(recipientUserId, orderId, type, title, body);
    }
}
