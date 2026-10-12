using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>Where a request came from: the address of the computer and its browser. Only the sign-in and sign-out pages know it.</summary>
public sealed record ClientInfo(string? IpAddress, string? UserAgent)
{
    public static ClientInfo From(HttpContext http) =>
        new(http.Connection.RemoteIpAddress?.ToString(), http.Request.Headers.UserAgent.ToString());
}

/// <summary>What the administrator filters the security log by. Every part is optional.</summary>
public sealed record SecurityLogFilter(string? Group = null, string? Search = null, DateOnly? From = null, DateOnly? To = null, string? Severity = null);

public sealed record SecurityLogRow(
    int Id, DateTime AtUtc, string Kind, string Severity, int? UserId, string? Email, string? ActorName, string? IpAddress, string? UserAgent, string? Details);

public sealed record SecurityLogPage(IReadOnlyList<SecurityLogRow> Items, int Total, int Page, int PageSize)
{
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>The numbers at the top of the Security Logs page (the last 24 hours).</summary>
public sealed record SecuritySummary(int SignIns, int FailedSignIns, int LockedAccounts, int Warnings, int ActiveNow);

/// <summary>
/// The security log: what happened to the accounts and the system, written by the sign-in page, by the account pages of the administrator
/// and by the backup and settings pages, and read by the administrator on the Security Logs page. A line that can't be written never stops
/// what it is about (a sign-in works even when the log can't be written).
/// </summary>
public sealed class SecurityLogService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock, ILogger<SecurityLogService> logger)
{
    /// <summary>The groups of events the page can filter by, in the order of the buttons.</summary>
    public static readonly (string Key, string Title, string[] Kinds)[] Groups =
    [
        ("signins", "Sign-ins", [SecurityEvents.SignInSucceeded, SecurityEvents.SignedOut]),
        ("failed", "Failed", [SecurityEvents.SignInFailed, SecurityEvents.AccountLocked, SecurityEvents.SignInRefused]),
        ("accounts", "Accounts", [SecurityEvents.AccountCreated, SecurityEvents.AccountChanged, SecurityEvents.PasswordReset, SecurityEvents.AccountActivated, SecurityEvents.AccountDeactivated, SecurityEvents.AccountDeleted]),
        ("system", "System", [SecurityEvents.BackupCreated, SecurityEvents.BackupDeleted, SecurityEvents.BackupRestored, SecurityEvents.BackupRestoreFailed, SecurityEvents.SettingsChanged, SecurityEvents.LogsCleared]),
    ];

    public async Task AddAsync(
        string kind, int? userId = null, string? email = null, string? details = null, ClientInfo? client = null,
        int? actorUserId = null, string? actorName = null, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            // The email of the account when only the account is known; the name of the actor when only the actor is known.
            if (email is null && userId is { } id)
            {
                email = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Email).FirstOrDefaultAsync(ct);
            }

            if (actorName is null && actorUserId is { } actor)
            {
                var people = await UserDirectory.LoadAsync(db, [actor], ct);
                actorName = people.TryGetValue(actor, out var person) ? person.Name : null;
            }

            db.SecurityLogs.Add(new SecurityLog
            {
                Kind = kind,
                Severity = SecurityEvents.SeverityOf(kind),
                UserId = userId,
                Email = Limit(email, 254),
                ActorUserId = actorUserId,
                ActorName = Limit(actorName, 200),
                IpAddress = Limit(client?.IpAddress, 45),
                UserAgent = Limit(client?.UserAgent, 300),
                Details = Limit(details, 1000),
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The security log line '{Kind}' could not be written.", kind);
        }
    }

    public async Task<SecurityLogPage> GetPageAsync(SecurityLogFilter filter, int page, int pageSize = 25, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query = Filtered(db, filter);

        var total = await query.CountAsync(ct);
        pageSize = Math.Clamp(pageSize, 5, 200);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pages);

        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc).ThenByDescending(l => l.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new SecurityLogRow(l.Id, l.CreatedAtUtc, l.Kind, l.Severity, l.UserId, l.Email, l.ActorName, l.IpAddress, l.UserAgent, l.Details))
            .ToListAsync(ct);

        return new SecurityLogPage(items, total, page, pageSize);
    }

    /// <summary>Every line that matches (for the file the administrator downloads), the newest first, at most <paramref name="max"/> of them.</summary>
    public async Task<IReadOnlyList<SecurityLogRow>> GetAllAsync(SecurityLogFilter filter, int max = 50_000, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await Filtered(db, filter)
            .OrderByDescending(l => l.CreatedAtUtc).ThenByDescending(l => l.Id)
            .Take(max)
            .Select(l => new SecurityLogRow(l.Id, l.CreatedAtUtc, l.Kind, l.Severity, l.UserId, l.Email, l.ActorName, l.IpAddress, l.UserAgent, l.Details))
            .ToListAsync(ct);
    }

    public async Task<SecuritySummary> GetSummaryAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var since = now.AddHours(-24);

        var counts = await db.SecurityLogs.AsNoTracking()
            .Where(l => l.CreatedAtUtc >= since)
            .GroupBy(l => l.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Count(string kind) => counts.Where(c => c.Kind == kind).Sum(c => c.Count);
        var warnings = await db.SecurityLogs.CountAsync(l => l.CreatedAtUtc >= since && l.Severity != "Info", ct);
        var locked = await db.Users.CountAsync(u => u.LockoutEndUtc != null && u.LockoutEndUtc > now, ct);

        return new SecuritySummary(Count(SecurityEvents.SignInSucceeded), Count(SecurityEvents.SignInFailed), locked, warnings, 0);
    }

    /// <summary>Deletes the lines older than this many days. Gives back how many were deleted.</summary>
    public async Task<int> PurgeAsync(int olderThanDays, CancellationToken ct = default)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-olderThanDays);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.SecurityLogs.Where(l => l.CreatedAtUtc < cutoff).ExecuteDeleteAsync(ct);
    }

    /// <summary>The administrator clears the lines older than this many days (30 at least, so the last month is always there).</summary>
    public async Task<ServiceResult> ClearOldAsync(int actingUserId, int olderThanDays, CancellationToken ct = default)
    {
        if (olderThanDays < 30)
        {
            return ServiceResult.Fail("The last 30 days of the log are always kept.");
        }

        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == actingUserId && u.IsActive && u.RoleId == AppRoles.AdminId, ct))
            {
                return ServiceResult.Fail("Only an administrator can clear the log.");
            }
        }

        var deleted = await PurgeAsync(olderThanDays, ct);
        await AddAsync(SecurityEvents.LogsCleared, actingUserId, details: $"{deleted} lines older than {olderThanDays} days were deleted.", actorUserId: actingUserId, ct: ct);
        return ServiceResult.Ok(deleted == 0 ? "There was nothing older than that." : $"Deleted {deleted} {(deleted == 1 ? "line" : "lines")}.");
    }

    private static IQueryable<SecurityLog> Filtered(AppDbContext db, SecurityLogFilter filter)
    {
        IQueryable<SecurityLog> query = db.SecurityLogs.AsNoTracking();

        if (Groups.FirstOrDefault(g => g.Key == filter.Group) is { Kinds: { } kinds })
        {
            query = query.Where(l => kinds.Contains(l.Kind));
        }

        if (filter.Severity is "Info" or "Warning" or "Critical")
        {
            var severity = filter.Severity;
            query = query.Where(l => l.Severity == severity);
        }

        if (filter.From is { } from)
        {
            // Dates are Philippine dates; the log keeps UTC.
            var start = PhilippineTime.ToUtc(from.ToDateTime(TimeOnly.MinValue));
            query = query.Where(l => l.CreatedAtUtc >= start);
        }

        if (filter.To is { } to)
        {
            var end = PhilippineTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(l => l.CreatedAtUtc < end);
        }

        foreach (var token in (filter.Search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5))
        {
            var t = token;
            query = query.Where(l =>
                (l.Email != null && l.Email.Contains(t)) || (l.ActorName != null && l.ActorName.Contains(t))
                || (l.IpAddress != null && l.IpAddress.Contains(t)) || (l.Details != null && l.Details.Contains(t)));
        }

        return query;
    }

    private static string? Limit(string? text, int max) => string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..max];
}
