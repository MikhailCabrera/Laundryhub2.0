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
    public const string Promo = "Promo";
    public const string RiderAssigned = "RiderAssigned";

    private readonly INotificationSender _sender;
    private readonly SemaphoreSmsSender? _sms;
    private readonly SesEmailSender? _email;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<NotificationService>? _logger;

    public NotificationService(INotificationSender sender)
    {
        _sender = sender;
    }

    // Additive overload: DI prefers this once the senders are registered.
    // The single-parameter constructor above is preserved unchanged.
    public NotificationService(
        INotificationSender sender,
        SemaphoreSmsSender sms,
        SesEmailSender email,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _sender = sender;
        _sms = sms;
        _email = email;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task NotifyAsync(string recipientUserId, int orderId, string type, string title, string body)
    {
        await _sender.SendAsync(recipientUserId, orderId, type, title, body);
        await FanOutAsync(recipientUserId, orderId, type, title, body);
    }

    // ── Additive fan-out helpers (new methods only) ──────────────────────

    private async Task FanOutAsync(string recipientUserId, int orderId, string type, string title, string body)
    {
        if (_sms != null && IsSmsEnabled(type))
        {
            try
            {
                await _sms.SendAsync(recipientUserId, orderId, type, title, body);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "SMS fan-out failed for type {Type}; in-app notification kept.", type);
            }
        }

        if (_email != null && IsEmailEnabled(type))
        {
            try
            {
                await _email.SendAsync(recipientUserId, orderId, type, title, body);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Email fan-out failed for type {Type}; in-app notification kept.", type);
            }
        }
    }

    private bool IsSmsEnabled(string type)
    {
        if (IsProviderDisabled(_configuration?["Sms:Provider"]))
            return false;
        return ParseEnabledTypes(
            _configuration?["Sms:EnabledEventTypes"],
            new[] { WeightConfirmed, ReadyForDelivery, OutForDelivery, Delivered })
            .Contains(type);
    }

    private bool IsEmailEnabled(string type)
    {
        if (IsProviderDisabled(_configuration?["Email:Provider"]))
            return false;
        return ParseEnabledTypes(
            _configuration?["Email:EnabledEventTypes"],
            new[] { PaymentConfirmed, Delivered })
            .Contains(type);
    }

    private static bool IsProviderDisabled(string? provider) =>
        string.Equals(provider, "None", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "Off", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "Disabled", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> ParseEnabledTypes(string? csv, string[] defaults)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return new HashSet<string>(defaults, StringComparer.OrdinalIgnoreCase);
        return new HashSet<string>(
            csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }
}
