using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SEAITT_LMS.Data;

namespace SEAITT_LMS.Auth;

public static class AuthCookieEvents
{
    /// <summary>
    /// A cookie is valid on its own, so without this a deactivated user would stay signed in until the
    /// cookie expires. Every <see cref="AuthOptions.RevalidateMinutes"/> the account is looked up again;
    /// if it is gone, inactive, or its security stamp changed, the cookie is rejected.
    /// </summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var services = context.HttpContext.RequestServices;
        var settings = services.GetRequiredService<IOptions<AuthOptions>>().Value;
        var clock = services.GetRequiredService<TimeProvider>();

        var issuedUtc = context.Properties.IssuedUtc;
        if (issuedUtc is not null && clock.GetUtcNow() - issuedUtc.Value < TimeSpan.FromMinutes(settings.RevalidateMinutes))
        {
            return;
        }

        var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = context.Principal?.FindFirstValue(AppClaimTypes.SecurityStamp);

        if (!int.TryParse(userIdClaim, out var userId) || string.IsNullOrEmpty(stamp))
        {
            await RejectAsync(context);
            return;
        }

        var dbFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(context.HttpContext.RequestAborted);

        var current = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive, u.SecurityStamp, u.ActiveSessionId, u.ActiveSessionSeenUtc })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (current is null || !current.IsActive || current.SecurityStamp != stamp)
        {
            await RejectAsync(context);
            return;
        }

        // The account was signed in somewhere else after this sign-in had not been seen for a while: this one is over.
        var session = context.Principal?.FindFirstValue(AppClaimTypes.SessionId);
        if (settings.SingleSession && !string.IsNullOrEmpty(session) && current.ActiveSessionId != session)
        {
            await RejectAsync(context);
            return;
        }

        // A page of this sign-in says every minute that it is open. When none has said so for a while the browser or the tab was closed: the sign-in is over (the person logs in again),
        // and the account is free for the next sign-in.
        if (settings.SingleSession && !string.IsNullOrEmpty(session) && current.ActiveSessionId == session && current.ActiveSessionSeenUtc is { } seen
            && clock.GetUtcNow().UtcDateTime - seen > TimeSpan.FromMinutes(Math.Max(1, settings.SessionIdleMinutes)))
        {
            await db.Users.Where(u => u.Id == userId && u.ActiveSessionId == session)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.ActiveSessionId, (string?)null).SetProperty(u => u.ActiveSessionSeenUtc, (DateTime?)null), context.HttpContext.RequestAborted);
            await RejectAsync(context);
            return;
        }

        // Re-issue the cookie so the next check is another RevalidateMinutes away.
        context.ShouldRenew = true;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
