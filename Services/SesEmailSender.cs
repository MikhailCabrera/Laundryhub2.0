using System.Net;
using System.Net.Mail;
using LaundryHub2._0.Data;

namespace LaundryHub2._0.Services;

/// <summary>
/// Amazon SES email sender over plain SMTP (System.Net.Mail, no AWS SDK)
/// behind the <see cref="INotificationSender"/> seam. Dry-run first: with
/// Email:DryRun=true (default) no SMTP connection is opened and a DRYRUN
/// email line is logged instead. Secrets are never logged.
/// </summary>
public class SesEmailSender : INotificationSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SesEmailSender> _logger;
    private readonly ApplicationDbContext _context;

    public SesEmailSender(
        IConfiguration configuration,
        ILogger<SesEmailSender> logger,
        ApplicationDbContext context)
    {
        _configuration = configuration;
        _logger = logger;
        _context = context;
    }

    public async Task SendAsync(string recipientUserId, int orderId, string type, string title, string body)
    {
        var address = await ResolveEmailAsync(recipientUserId);
        if (string.IsNullOrWhiteSpace(address) || !address.Contains('@'))
        {
            _logger.LogInformation("Email skip: missing email for user {UserId} type {Type}; notification stays in-app.", recipientUserId, type);
            return;
        }

        var dryRun = _configuration.GetValue<bool>("Email:DryRun", true);
        var preview = Truncate60(body);
        if (dryRun)
        {
            _logger.LogInformation("DRYRUN email to={Addr} subject={Subject} body={Body}", address, title, preview);
            return;
        }

        var region = _configuration["Email:Ses:Region"];
        var fromAddress = _configuration["Email:Ses:FromAddress"];
        var replyTo = _configuration["Email:Ses:ReplyTo"];
        var username = _configuration["Email:Ses:AccessKeyId"];
        var password = _configuration["Email:Ses:SecretAccessKey"];
        var configurationSet = _configuration["Email:Ses:ConfigurationSet"];

        if (string.IsNullOrWhiteSpace(region))
            region = "ap-southeast-1";
        if (string.IsNullOrWhiteSpace(fromAddress) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogError("Email not sent: Email:Ses:FromAddress/AccessKeyId/SecretAccessKey incomplete (type {Type} to {Addr}).", type, address);
            return;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress),
                Subject = title,
                Body = body,
                IsBodyHtml = false,
            };
            message.To.Add(address);
            if (!string.IsNullOrWhiteSpace(replyTo))
                message.ReplyToList.Add(replyTo);
            if (!string.IsNullOrWhiteSpace(configurationSet))
                message.Headers.Add("X-SES-CONFIGURATION-SET", configurationSet);

            var host = $"email-smtp.{region}.amazonaws.com";
            using var client = new SmtpClient(host, 587)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(username, password),
                Timeout = 30000,
            };
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            // Sender failures must never fail the caller; log and continue.
            _logger.LogWarning(ex, "SES email error for type {Type} to {Addr}.", type, address);
        }
    }

    private async Task<string?> ResolveEmailAsync(string recipientUserId)
    {
        try
        {
            var user = await _context.Users.FindAsync(recipientUserId);
            return user?.Email;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Email skip: address lookup failed for user {UserId}.", recipientUserId);
            return null;
        }
    }

    internal static string Truncate60(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        var flat = value.Replace("\r", " ").Replace("\n", " ");
        return flat.Length <= 60 ? flat : flat.Substring(0, 60);
    }
}
