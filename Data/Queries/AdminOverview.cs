using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data.Queries;

/// <summary>How many accounts a role has, and how many of those cannot sign in (deactivated).</summary>
public sealed record RoleTotal(string Role, int Total, int Inactive);

/// <summary>One account in the "Latest accounts" list.</summary>
public sealed record AccountRow(string Name, string Email, string Role, bool IsActive, bool IsLocked, DateTime? LastLoginAtUtc);

/// <summary>How many rows the school setup tables have.</summary>
public sealed record SetupTotals(
    int SchoolYears, int Terms, int Tracks, int Strands, int Subjects, int Sections, int Enrollments);

/// <summary>Everything the admin dashboard shows.</summary>
public sealed record AdminOverview(IReadOnlyList<RoleTotal> Roles, SetupTotals Setup, IReadOnlyList<AccountRow> LatestAccounts)
{
    public int TotalAccounts => Roles.Sum(r => r.Total);

    public int InactiveAccounts => Roles.Sum(r => r.Inactive);

    private static readonly string[] RoleOrder =
        [AppRoles.Admin, AppRoles.Teacher, AppRoles.Student, AppRoles.Registrar, AppRoles.GuidanceCounselor];

    public static async Task<AdminOverview> LoadAsync(AppDbContext db, DateTime nowUtc, int latest = 8, CancellationToken ct = default)
    {
        var perRole = await db.Users
            .AsNoTracking()
            .GroupBy(u => u.RoleId)
            .Select(g => new { RoleId = g.Key, Total = g.Count(), Inactive = g.Count(u => !u.IsActive) })
            .ToListAsync(ct);

        // Every role is listed, also the ones nobody has yet.
        var roles = RoleOrder
            .Select(name =>
            {
                var row = perRole.FirstOrDefault(r => r.RoleId == AppRoles.Ids[name]);
                return new RoleTotal(name, row?.Total ?? 0, row?.Inactive ?? 0);
            })
            .ToList();

        var setup = new SetupTotals(
            await db.SchoolYears.CountAsync(ct),
            await db.Terms.CountAsync(ct),
            await db.Tracks.CountAsync(ct),
            await db.Strands.CountAsync(ct),
            await db.Subjects.CountAsync(ct),
            await db.Sections.CountAsync(ct),
            await db.Enrollments.CountAsync(ct));

        var users = await db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.Administrator)
            .Include(u => u.GuidanceCounselor)
            .Include(u => u.Registrar)
            .Include(u => u.Teacher)
            .Include(u => u.Student)
            .OrderByDescending(u => u.CreatedAtUtc)
            .ThenByDescending(u => u.Id)
            .Take(latest)
            .ToListAsync(ct);

        var accounts = users
            .Select(u => new AccountRow(
                u.DisplayName, u.Email, AppRoles.DisplayName(u.Role.Name), u.IsActive, u.LockoutEndUtc > nowUtc, u.LastLoginAtUtc))
            .ToList();

        return new AdminOverview(roles, setup, accounts);
    }
}
