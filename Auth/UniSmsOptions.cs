namespace SEAITT_LMS.Auth;

/// <summary>
/// Settings of the UniSMS service that sends the one-time codes (round 42), bound from the "UniSms" section.
/// <b>The API key is a secret and is never in the repository:</b> it comes from the user secrets of the project
/// (<c>dotnet user-secrets set "UniSms:ApiKey" "..."</c>), from <c>appsettings.Local.json</c> (git ignores it) or from the environment variable <c>UniSms__ApiKey</c>.
/// </summary>
public sealed class UniSmsOptions
{
    public const string SectionName = "UniSms";

    /// <summary>The secret key of the API ("SEAITT LMS Authentication" in the UniSMS dashboard). Empty: no message can be sent, and the OTP setting can't be turned on.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>The name the message comes from (it has to be registered in UniSMS).</summary>
    public string SenderId { get; set; } = "Unisoft";

    /// <summary>The address that sends a message (POST, JSON). Change it only if UniSMS says so.</summary>
    public string Endpoint { get; set; } = "https://unismsapi.com/api/sms";

    /// <summary>"Basic" (the key is the user name and the password is empty) or "Bearer" (the key is the token).</summary>
    public string AuthScheme { get; set; } = "Basic";

    /// <summary>The names of the fields of the JSON that is sent. Change them only if the UniSMS documentation uses other names.</summary>
    public string RecipientField { get; set; } = "recipient";

    public string ContentField { get; set; } = "content";

    public string SenderIdField { get; set; } = "sender_id";

    /// <summary>Seconds to wait for UniSMS to answer.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>The key is set and the address is https (the key travels in a header, so it is never sent over plain http; only an address on this computer, used by the tests, may be http).</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
}

/// <summary>What the one-time code does, bound from the "Otp" section: how long it lives, how many wrong tries it takes, how often a new one can be asked for.</summary>
public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    public int CodeLength { get; set; } = 6;

    /// <summary>Minutes a code can be used.</summary>
    public int ExpiryMinutes { get; set; } = 5;

    /// <summary>Wrong codes before the code is cancelled and the person logs in again with the password.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Seconds before a new code can be asked for.</summary>
    public int ResendSeconds { get; set; } = 60;

    /// <summary>How many codes one sign-in can use (the first one and the new ones).</summary>
    public int MaxSends { get; set; } = 4;
}
