using System.Text;
using System.Text.Json;
using LaundryHub2._0.Data;

namespace LaundryHub2._0.Services;

/// <summary>
/// Semaphore SMS sender behind the <see cref="INotificationSender"/> seam.
/// Dry-run first: with Sms:DryRun=true (default) no HTTP is issued and a
/// DRYRUN sms line is logged instead. API keys are never logged.
/// </summary>
public class SemaphoreSmsSender : INotificationSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SemaphoreSmsSender> _logger;
    private readonly ApplicationDbContext _context;

    public SemaphoreSmsSender(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SemaphoreSmsSender> logger,
        ApplicationDbContext context)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _context = context;
    }

    public async Task SendAsync(string recipientUserId, int orderId, string type, string title, string body)
    {
        var rawPhone = await ResolvePhoneAsync(recipientUserId, orderId);
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            _logger.LogInformation("SMS skip: missing phone for user {UserId} type {Type}; notification stays in-app.", recipientUserId, type);
            return;
        }

        var phone = NormalizePhone(rawPhone);
        if (phone == null)
        {
            _logger.LogInformation("SMS skip: invalid phone for user {UserId} type {Type}; notification stays in-app.", recipientUserId, type);
            return;
        }

        var dryRun = _configuration.GetValue<bool>("Sms:DryRun", true);
        var preview = Truncate60(body);
        if (dryRun)
        {
            _logger.LogInformation("DRYRUN sms to={Phone} type={Type} body={Body}", phone, type, preview);
            return;
        }

        var apiKey = _configuration["Sms:Semaphore:ApiKey"];
        var baseUrl = _configuration["Sms:Semaphore:BaseUrl"];
        var senderName = _configuration["Sms:Semaphore:SenderName"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("SMS not sent: Sms:Semaphore:ApiKey is not configured (type {Type} to {Phone}).", type, phone);
            return;
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "https://api.semaphore.co/api/v4/messages";

        var timeoutSeconds = _configuration.GetValue<int>("Sms:Semaphore:TimeoutSeconds", 30);
        if (timeoutSeconds <= 0 || timeoutSeconds > 120)
            timeoutSeconds = 30;

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var payload = new Dictionary<string, string>
            {
                ["apikey"] = apiKey,
                ["number"] = phone,
                ["message"] = $"{title}: {body}",
            };
            if (!string.IsNullOrWhiteSpace(senderName))
                payload["sendername"] = senderName;

            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            using var response = await client.SendAsync(request, cts.Token);
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                // Never log keys; truncate gateway body for debugging only.
                _logger.LogWarning("Semaphore SMS failed ({StatusCode}) for type {Type} to {Phone}: {Body}",
                    response.StatusCode, type, phone, Truncate(responseBody, 200));
            }
        }
        catch (Exception ex)
        {
            // Sender failures must never fail the caller; log and continue.
            _logger.LogWarning(ex, "Semaphore SMS error for type {Type} to {Phone}.", type, phone);
        }
    }

    private async Task<string?> ResolvePhoneAsync(string recipientUserId, int orderId)
    {
        try
        {
            var user = await _context.Users.FindAsync(recipientUserId);
            if (user != null && !string.IsNullOrWhiteSpace(user.PhoneNumber))
                return user.PhoneNumber;
            if (orderId > 0)
            {
                var order = await _context.LaundryOrders.FindAsync(orderId);
                if (order != null && !string.IsNullOrWhiteSpace(order.ContactNumber))
                    return order.ContactNumber;
            }
            return user?.PhoneNumber;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMS skip: phone lookup failed for user {UserId}.", recipientUserId);
            return null;
        }
    }

    /// <summary>
    /// Normalizes Philippine mobile numbers to E.164 (+63XXXXXXXXXX).
    /// Returns null when the input cannot be interpreted as a PH mobile.
    /// </summary>
    public static string? NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        string normalized;
        if (raw.TrimStart().StartsWith("+"))
        {
            normalized = "+" + digits;
        }
        else if (digits.Length == 11 && digits.StartsWith("09"))
        {
            normalized = "+63" + digits.Substring(1);
        }
        else if (digits.Length == 10 && digits.StartsWith("9"))
        {
            normalized = "+63" + digits;
        }
        else if (digits.Length == 12 && digits.StartsWith("63"))
        {
            normalized = "+" + digits;
        }
        else
        {
            return null;
        }

        if (normalized.Length != 13 || !normalized.StartsWith("+63"))
            return null;
        return normalized;
    }

    internal static string Truncate60(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        var flat = value.Replace("\r", " ").Replace("\n", " ");
        return flat.Length <= 60 ? flat : flat.Substring(0, 60);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value.Substring(0, maxLength) + "…";
}
