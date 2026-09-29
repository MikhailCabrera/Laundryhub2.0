using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LaundryHub2._0.Data;
using LaundryHub2._0.Models;
using LaundryHub2._0.Services;

namespace LaundryHub2._0.Controllers;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[Route("PayMongo")]
public class PayMongoController : Controller
{
    private const string CheckoutSessionPaidEvent = "checkout_session.payment.paid";
    private const string PaymentPaidEvent = "payment.paid";

    private readonly ApplicationDbContext _context;
    private readonly PayMongoService _payMongoService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayMongoController> _logger;
    private readonly NotificationService _notificationService;
    private readonly LoyaltyService _loyaltyService;

    public PayMongoController(
        ApplicationDbContext context,
        PayMongoService payMongoService,
        IConfiguration configuration,
        ILogger<PayMongoController> logger,
        NotificationService notificationService,
        LoyaltyService loyaltyService)
    {
        _context = context;
        _payMongoService = payMongoService;
        _configuration = configuration;
        _logger = logger;
        _notificationService = notificationService;
        _loyaltyService = loyaltyService;
    }

    [HttpPost("Webhook")]
    public async Task<IActionResult> Webhook()
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            rawBody = await reader.ReadToEndAsync();

        var secret = _configuration["PayMongo:WebhookSecret"];
        var signature = Request.Headers["Paymongo-Signature"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(secret) || !SignaturesMatch(secret, rawBody, signature))
        {
            _logger.LogWarning("Rejected PayMongo webhook with missing or invalid signature.");
            return Json(new { received = true });
        }

        string? eventId;
        string? eventType;
        string? sessionId = null;
        string? referenceNumber = null;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (!doc.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("id", out var idProp)
                || !data.TryGetProperty("attributes", out var attributes)
                || attributes.ValueKind != JsonValueKind.Object
                || !attributes.TryGetProperty("type", out var typeProp))
            {
                _logger.LogWarning("Rejected PayMongo webhook with unrecognized payload shape.");
                return Json(new { received = true });
            }
            eventId = idProp.GetString();
            eventType = typeProp.GetString();
            if (attributes.TryGetProperty("data", out var resource) && resource.ValueKind == JsonValueKind.Object)
            {
                if (resource.TryGetProperty("id", out var resourceId))
                    sessionId = resourceId.GetString();
                if (resource.TryGetProperty("attributes", out var resourceAttributes)
                    && resourceAttributes.ValueKind == JsonValueKind.Object
                    && resourceAttributes.TryGetProperty("reference_number", out var referenceProp))
                    referenceNumber = referenceProp.GetString();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Rejected PayMongo webhook with unparsable JSON payload.");
            return Json(new { received = true });
        }

        if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(eventType))
        {
            _logger.LogWarning("Rejected PayMongo webhook with missing event id or type.");
            return Json(new { received = true });
        }

        if (string.Equals(eventType, PaymentPaidEvent, StringComparison.Ordinal))
        {
            _logger.LogInformation("Ignored PayMongo webhook event {EventId}: payment-scoped events carry no order linkage and never drive confirmation.", eventId);
            return Json(new { received = true });
        }

        if (!string.Equals(eventType, CheckoutSessionPaidEvent, StringComparison.Ordinal))
        {
            _logger.LogInformation("Ignored PayMongo webhook event {EventId} of type {EventType}.", eventId, eventType);
            return Json(new { received = true });
        }

        LaundryOrder? order = null;
        if (!string.IsNullOrEmpty(sessionId))
            order = await _context.LaundryOrders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.PayMongoPaymentId == sessionId);
        if (order == null && !string.IsNullOrEmpty(referenceNumber))
            order = await _context.LaundryOrders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.OrderNumber == referenceNumber);
        if (order == null)
        {
            _logger.LogWarning("PayMongo webhook event {EventId} matched no order.", eventId);
            return Json(new { received = true });
        }

        if (order.IsPaymentConfirmed)
        {
            _logger.LogInformation("PayMongo webhook event {EventId} for order {OrderNumber} ignored: already confirmed.", eventId, order.OrderNumber);
            return Json(new { received = true });
        }

        var amountDue = await _loyaltyService.ComputeAmountDueAsync(order);
        var verification = await _payMongoService.VerifySessionPaidAsync(order.PayMongoPaymentId ?? string.Empty, amountDue);
        if (!verification.Paid)
        {
            _logger.LogWarning("PayMongo webhook event {EventId} for order {OrderNumber} failed re-verification: {Reason}", eventId, order.OrderNumber, verification.Reason);
            return Json(new { received = true });
        }

        var now = DateTime.UtcNow;
        var applied = OrderPaymentConfirmation.ApplyConfirmedPayment(order, now);
        if (!applied)
        {
            _logger.LogWarning("PayMongo webhook event {EventId} for order {OrderNumber}: ApplyConfirmedPayment declined (status={Status}).",
                eventId, order.OrderNumber, order.Status);
            return Json(new { received = true });
        }
        order.PayMongoWebhookEventId = eventId;
        order.PayMongoWebhookReceivedAt = now;
        AuditTrail.Record(_context, "PayMongo webhook", AuditTrail.Payment, order.Customer?.FullName ?? "Unknown", "Customer", $"Order {order.OrderNumber} paid via webhook event {eventId}.");
        if (order.Status == OrderStatus.ReadyForDelivery)
            await _notificationService.NotifyAsync(order.CustomerId, order.Id, NotificationService.ReadyForDelivery, $"Order {order.OrderNumber} ready for delivery", "Your laundry is ready and will be dispatched for delivery.");
        else
            await _notificationService.NotifyAsync(order.CustomerId, order.Id, NotificationService.PaymentConfirmed, $"Payment confirmed for order {order.OrderNumber}", "Payment received. Thank you!");
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            var fresh = await _context.LaundryOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == order.Id);
            if (fresh != null && fresh.IsPaymentConfirmed)
            {
                _logger.LogInformation("PayMongo webhook event {EventId} for order {OrderNumber} arrived after confirmation; treated as duplicate.", eventId, order.OrderNumber);
                return Json(new { received = true });
            }
            _logger.LogWarning("PayMongo webhook event {EventId} for order {OrderNumber} conflicted with another update and the order is not confirmed.", eventId, order.OrderNumber);
            return StatusCode(500, new { received = false, error = "Concurrent update conflict; payment state unknown." });
        }

        _logger.LogInformation("Order {OrderNumber} confirmed paid via PayMongo webhook event {EventId}.", order.OrderNumber, eventId);
        return Json(new { received = true });
    }

    private static bool SignaturesMatch(string secret, string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        var provided = signatureHeader.Trim().ToLowerInvariant();
        if (expected.Length != provided.Length) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(provided));
    }
}
