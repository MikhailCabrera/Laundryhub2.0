using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LaundryHub2._0.Services;

public class PayMongoService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PayMongoService> _logger;

    public PayMongoService(HttpClient httpClient, IConfiguration configuration, ILogger<PayMongoService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PayMongoCheckoutResult?> CreateCheckoutSessionAsync(
        int orderId,
        string orderNumber,
        decimal amountInPesos,
        string description,
        string successUrl,
        string cancelUrl)
    {
        var secretKey = _configuration["PayMongo:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogError("PayMongo:SecretKey is not configured; cannot create a checkout session. "
                + "Configure it via 'dotnet user-secrets set \"PayMongo:SecretKey\" \"<key>\"' or the PayMongo__SecretKey environment variable.");
            return null;
        }

        // PayMongo expects amounts in centavos (PHP 1.00 = 100 centavos)
        var amountInCentavos = (long)Math.Round(amountInPesos * 100m, MidpointRounding.AwayFromZero);

        var requestBody = new
        {
            data = new
            {
                attributes = new
                {
                    billing = new
                    {
                        name = "LaundryHub Customer"
                    },
                    send_email_receipt = false,
                    show_description = true,
                    show_line_items = true,
                    description = description,
                    line_items = new[]
                    {
                        new
                        {
                            amount = amountInCentavos,
                            currency = "PHP",
                            name = $"Laundry Service - {orderNumber}",
                            quantity = 1
                        }
                    },
                    payment_method_types = new[]
                    {
                        "card",
                        "gcash",
                        "paymaya",
                        "dob",
                        "billease"
                    },
                    success_url = successUrl,
                    cancel_url = cancelUrl,
                    reference_number = orderNumber
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.paymongo.com/v1/checkout_sessions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var authBytes = Encoding.ASCII.GetBytes($"{secretKey}:");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // Truncated: full gateway bodies may contain sensitive data; log the tail for debugging only.
                _logger.LogError("PayMongo checkout session creation failed ({StatusCode}): {Body}",
                    response.StatusCode, Truncate(responseString, 500));
                return null;
            }

            using var doc = JsonDocument.Parse(responseString);
            var dataObj = doc.RootElement.GetProperty("data");
            var checkoutId = dataObj.GetProperty("id").GetString();
            var checkoutUrl = dataObj.GetProperty("attributes").GetProperty("checkout_url").GetString();

            return new PayMongoCheckoutResult
            {
                CheckoutSessionId = checkoutId ?? string.Empty,
                CheckoutUrl = checkoutUrl ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call PayMongo checkout session API");
            return null;
        }
    }

    /// <summary>
    /// Verifies a checkout session actually resulted in a paid payment of at
    /// least <paramref name="expectedAmountInPesos"/>. Fail-closed: any missing
    /// key, API error, or unrecognized payload shape returns Paid = false and
    /// the caller must NOT mark the order paid.
    /// </summary>
    public async Task<PayMongoVerificationResult> VerifySessionPaidAsync(
        string checkoutSessionId,
        decimal expectedAmountInPesos)
    {
        var secretKey = _configuration["PayMongo:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogError("PayMongo:SecretKey is not configured; refusing to verify payment. "
                + "Configure it via 'dotnet user-secrets set \"PayMongo:SecretKey\" \"<key>\"' or the PayMongo__SecretKey environment variable.");
            return new PayMongoVerificationResult { Paid = false, Reason = "Payment gateway is not configured." };
        }

        if (string.IsNullOrWhiteSpace(checkoutSessionId))
            return new PayMongoVerificationResult { Paid = false, Reason = "No checkout session recorded for this order." };

        var expectedCentavos = (long)Math.Round(expectedAmountInPesos * 100m, MidpointRounding.AwayFromZero);

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"https://api.paymongo.com/v1/checkout_sessions/{checkoutSessionId}");
            var authBytes = Encoding.ASCII.GetBytes($"{secretKey}:");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayMongo session verification failed ({StatusCode}): {Body}",
                    response.StatusCode, Truncate(responseString, 500));
                return new PayMongoVerificationResult { Paid = false, Reason = "Payment could not be verified with the gateway. Please try again." };
            }

            using var doc = JsonDocument.Parse(responseString);
            if (!doc.RootElement.TryGetProperty("data", out var dataObj)
                || !dataObj.TryGetProperty("attributes", out var attrs)
                || !attrs.TryGetProperty("payments", out var payments)
                || payments.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("PayMongo session {SessionId} has an unrecognized payload shape; refusing to confirm payment.", checkoutSessionId);
                return new PayMongoVerificationResult { Paid = false, Reason = "Payment could not be verified with the gateway. Please try again." };
            }

            foreach (var payment in payments.EnumerateArray())
            {
                // Elements may be expanded payment objects or plain IDs.
                string? paymentId = null;
                string? status = null;
                long amount = 0;
                if (payment.ValueKind == JsonValueKind.Object)
                {
                    if (payment.TryGetProperty("id", out var idProp)) paymentId = idProp.GetString();
                    if (payment.TryGetProperty("attributes", out var payAttrs))
                    {
                        if (payAttrs.TryGetProperty("status", out var statusProp)) status = statusProp.GetString();
                        if (payAttrs.TryGetProperty("amount", out var amountProp) && amountProp.TryGetInt64(out var a)) amount = a;
                    }
                }
                else if (payment.ValueKind == JsonValueKind.String)
                {
                    paymentId = payment.GetString();
                }

                // Unexpanded ID: retrieve the payment to read its status.
                if (status == null && !string.IsNullOrEmpty(paymentId))
                {
                    var fetched = await GetPaymentAsync(paymentId, secretKey);
                    status = fetched?.Status;
                    amount = fetched?.Amount ?? 0;
                }

                if (string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase))
                {
                    if (amount >= expectedCentavos)
                        return new PayMongoVerificationResult { Paid = true, PaidAmountCentavos = amount, PaidPaymentId = paymentId };
                    _logger.LogWarning("PayMongo session {SessionId} paid {Paid} centavos, below expected {Expected}; refusing to confirm.",
                        checkoutSessionId, amount, expectedCentavos);
                    return new PayMongoVerificationResult { Paid = false, Reason = "The recorded payment does not cover the amount due. Please contact support." };
                }
            }

            return new PayMongoVerificationResult { Paid = false, Reason = "No completed payment found for this checkout session yet." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify PayMongo checkout session {SessionId}", checkoutSessionId);
            return new PayMongoVerificationResult { Paid = false, Reason = "Payment could not be verified with the gateway. Please try again." };
        }
    }

    private async Task<(string? Status, long Amount)?> GetPaymentAsync(string paymentId, string secretKey)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"https://api.paymongo.com/v1/payments/{paymentId}");
        var authBytes = Encoding.ASCII.GetBytes($"{secretKey}:");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var dataObj)
            || !dataObj.TryGetProperty("attributes", out var attrs))
            return null;
        string? status = attrs.TryGetProperty("status", out var s) ? s.GetString() : null;
        long amount = attrs.TryGetProperty("amount", out var a) && a.TryGetInt64(out var v) ? v : 0;
        return (status, amount);
    }

    public async Task<PayMongoRefundResult> RefundPaymentAsync(string paymentId, long amountCentavos)
    {
        var secretKey = _configuration["PayMongo:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogError("PayMongo:SecretKey is not configured; refusing to refund payment. "
                + "Configure it via 'dotnet user-secrets set \"PayMongo:SecretKey\" \"<key>\"' or the PayMongo__SecretKey environment variable.");
            return new PayMongoRefundResult { Succeeded = false, Reason = "Payment gateway is not configured." };
        }

        if (string.IsNullOrWhiteSpace(paymentId) || amountCentavos <= 0)
            return new PayMongoRefundResult { Succeeded = false, Reason = "Invalid payment reference or refund amount." };

        var requestBody = new
        {
            data = new
            {
                attributes = new
                {
                    payment_id = paymentId,
                    amount = amountCentavos,
                    reason = "requested_by_customer"
                }
            }
        };

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.paymongo.com/v1/refunds")
            {
                Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };
            var authBytes = Encoding.ASCII.GetBytes($"{secretKey}:");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayMongo refund failed ({StatusCode}): {Body}",
                    response.StatusCode, Truncate(responseString, 500));
                return new PayMongoRefundResult { Succeeded = false, Reason = "The refund was rejected by the payment gateway." };
            }

            using var doc = JsonDocument.Parse(responseString);
            if (!doc.RootElement.TryGetProperty("data", out var dataObj)
                || dataObj.ValueKind != JsonValueKind.Object
                || !dataObj.TryGetProperty("id", out var idProp))
            {
                _logger.LogWarning("PayMongo refund response has an unrecognized shape; refusing to record a refund.");
                return new PayMongoRefundResult { Succeeded = false, Reason = "The refund could not be confirmed with the gateway." };
            }

            return new PayMongoRefundResult { Succeeded = true, RefundId = idProp.GetString() };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call PayMongo refunds API for payment {PaymentId}", paymentId);
            return new PayMongoRefundResult { Succeeded = false, Reason = "The refund could not be processed. Please try again." };
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value.Substring(0, maxLength) + "…";
}

public class PayMongoCheckoutResult
{
    public string CheckoutSessionId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
}

public class PayMongoVerificationResult
{
    public bool Paid { get; set; }
    public long PaidAmountCentavos { get; set; }
    public string? PaidPaymentId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class PayMongoRefundResult
{
    public bool Succeeded { get; set; }
    public string? RefundId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
