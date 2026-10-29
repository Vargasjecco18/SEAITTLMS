using System.Net;
using System.Net.Mail;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace SEAITT_LMS.Services;

/// <summary>
/// How the system sends email, from the "Email" section of appsettings (see appsettings.Local.example.json). It is used to send the notifications of a person to the email address
/// they added in Settings (a Gmail, for example). Without a Host the system sends nothing: the addresses are kept and the emails start once it is set up.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>The mail server, like smtp.gmail.com. Empty: no email is sent.</summary>
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary>The account that sends (for Gmail: the address, with an app password as the password).</summary>
    public string? User { get; set; }

    public string? Password { get; set; }

    /// <summary>The address the emails come from (the User when empty).</summary>
    public string? From { get; set; }

    public bool EnableSsl { get; set; } = true;

    /// <summary>The address of the system on the internet, like https://lms.school.edu: the emails have a link to the page of the notification.</summary>
    public string? PublicUrl { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From ?? User);
}

public sealed record EmailMessage(string To, string Subject, string Text);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>Sends with an SMTP server (Gmail, Outlook, the school's own).</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        using var client = new SmtpClient(o.Host, o.Port)
        {
            EnableSsl = o.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 20_000,
            Credentials = string.IsNullOrWhiteSpace(o.User) ? null : new NetworkCredential(o.User, o.Password),
        };
        using var mail = new MailMessage(new MailAddress(o.From ?? o.User!, "SEAITT Legazpi LMS"), new MailAddress(message.To))
        {
            Subject = message.Subject,
            Body = message.Text,
            IsBodyHtml = false,
        };
        await client.SendMailAsync(mail, ct);
    }
}

/// <summary>
/// The emails waiting to be sent. The part of the system that makes a notification only puts a message here and goes on (it never waits for the mail server);
/// <see cref="EmailWorker"/> sends them one by one. At most 1,000 wait; if more come the oldest are dropped.
/// </summary>
public sealed class EmailQueue(IOptions<EmailOptions> options)
{
    private readonly Channel<EmailMessage> channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public bool Enabled => options.Value.Enabled;

    public string? PublicUrl => options.Value.PublicUrl?.TrimEnd('/');

    public ChannelReader<EmailMessage> Reader => channel.Reader;

    /// <summary>Puts a message in the queue. False when email is not set up (nothing is kept).</summary>
    public bool TryEnqueue(EmailMessage message) => Enabled && channel.Writer.TryWrite(message);

    /// <summary>The words of the email of a notification: what happened, where to see it, and why the person gets it.</summary>
    public EmailMessage ForNotification(string to, string title, string? body, string? link)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(body))
        {
            lines.Add(body.Trim());
        }

        if (!string.IsNullOrWhiteSpace(link) && PublicUrl is { Length: > 0 } root)
        {
            lines.Add($"See it here: {root}/{link.TrimStart('/')}");
        }

        lines.Add("");
        lines.Add("You get this email because you added this address in the Settings of SEAITT Legazpi LMS. To stop, remove the address there.");
        return new EmailMessage(to, title, string.Join(Environment.NewLine, lines));
    }
}

/// <summary>Sends the emails of <see cref="EmailQueue"/>. A message that can't be sent is written in the log and left out: a failing mail server never stops the system.</summary>
public sealed class EmailWorker(EmailQueue queue, IEmailSender sender, ILogger<EmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await sender.SendAsync(message, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "The email to {Address} could not be sent.", message.To);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
