using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SEAITT_LMS.Auth;

namespace SEAITT_LMS.Services;

/// <summary>What UniSMS answered to a message.</summary>
/// <param name="Succeeded">True when UniSMS accepted the message (an answer 2xx).</param>
/// <param name="Error">When it did not: a short sentence that tells why (never the key).</param>
public sealed record SmsResult(bool Succeeded, string? Error = null)
{
    public static SmsResult Ok() => new(true);

    public static SmsResult Fail(string error) => new(false, error);
}

/// <summary>
/// Sends a text message through UniSMS (https://unismsapi.com): one POST with the number, the words and the sender id, the API key in the Authorization header.
/// The key comes from <see cref="UniSmsOptions"/> and is never written in the log or in an answer.
/// </summary>
public sealed class UniSmsClient(HttpClient http, IOptions<UniSmsOptions> options, ILogger<UniSmsClient> logger)
{
    // the plus of +63... and the letters of the message are written as they are (not as \u002B)
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public bool IsConfigured => options.Value.IsConfigured;

    public string SenderId => options.Value.SenderId;

    /// <param name="recipient">The number in E.164 (+639171234567).</param>
    public async Task<SmsResult> SendAsync(string recipient, string content, CancellationToken ct = default)
    {
        var o = options.Value;
        if (!o.IsConfigured)
        {
            return SmsResult.Fail("UniSMS is not set up: the API key is missing.");
        }

        var body = new Dictionary<string, string> { [o.RecipientField] = recipient, [o.ContentField] = content };
        if (!string.IsNullOrWhiteSpace(o.SenderId))
        {
            body[o.SenderIdField] = o.SenderId.Trim();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, o.Endpoint) { Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json") };
        request.Headers.Authorization = string.Equals(o.AuthScheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            ? new AuthenticationHeaderValue("Bearer", o.ApiKey.Trim())
            : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(o.ApiKey.Trim() + ":")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 3, 60)));
            using var response = await http.SendAsync(request, timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("UniSMS accepted a message for {Recipient}.", Mask(recipient));
                return SmsResult.Ok();
            }

            var text = await response.Content.ReadAsStringAsync(ct);
            var why = Reason(text);
            logger.LogWarning("UniSMS refused a message for {Recipient}: {Status} {Why}", Mask(recipient), (int)response.StatusCode, why);
            return SmsResult.Fail($"UniSMS answered {(int)response.StatusCode}{(string.IsNullOrEmpty(why) ? "" : ": " + why)}.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("UniSMS did not answer in time.");
            return SmsResult.Fail("UniSMS did not answer in time.");
        }
        catch (HttpRequestException e)
        {
            logger.LogWarning("UniSMS could not be reached: {Message}", e.Message);
            return SmsResult.Fail("UniSMS could not be reached (check the internet connection).");
        }
    }

    /// <summary>+639171234567 becomes +6391***4567: enough for a person to know which phone, not enough to read the number.</summary>
    public static string Mask(string? number)
    {
        var n = number ?? "";
        return n.Length < 10 ? "***" : n[..5] + "***" + n[^4..];
    }

    /// <summary>The sentence UniSMS gave ("message" or "error" of its JSON), cut short; empty when it gave none.</summary>
    private static string Reason(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var name in new[] { "message", "error", "detail", "title" })
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var s = value.GetString() ?? "";
                    return s.Length > 160 ? s[..160] : s;
                }
            }
        }
        catch (JsonException)
        {
        }

        var plain = json.Trim();
        return plain.Length is > 0 and <= 160 && !plain.StartsWith('<') ? plain : "";
    }
}
