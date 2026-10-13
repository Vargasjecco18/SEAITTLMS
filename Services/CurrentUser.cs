using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;

namespace SEAITT_LMS.Services;

/// <summary>Who is signed in, as the pages and services need to know it.</summary>
public sealed record CurrentUserInfo(int UserId, string Role, string Name, string Email);

/// <summary>
/// The signed-in person for the page that is open, read from the sign-in cookie. A teacher's or student's own record id
/// (not the account id) is looked up once and kept for the page.
/// </summary>
public sealed class CurrentUser(AuthenticationStateProvider authentication, IDbContextFactory<AppDbContext> dbFactory)
{
    private CurrentUserInfo? info;

    public async Task<CurrentUserInfo?> GetAsync()
    {
        if (info is not null)
        {
            return info;
        }

        var user = (await authentication.GetAuthenticationStateAsync()).User;
        if (user.Identity?.IsAuthenticated != true
            || !int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return null;
        }

        info = new CurrentUserInfo(id, user.FindFirstValue(ClaimTypes.Role) ?? "", user.Identity?.Name ?? "", user.FindFirstValue(ClaimTypes.Email) ?? "");
        return info;
    }

    // Several live parts of one page (the widgets, the boxes) ask at the same time: they all wait for the same lookup, instead of the second one
    // being told "nobody" while the first is still asking the database.
    private Task<int?>? studentLookup;
    private Task<int?>? teacherLookup;

    /// <summary>The id of the signed-in student's record in the students table, or null when the signed-in person is not a student.</summary>
    public Task<int?> GetStudentIdAsync() => studentLookup ??= LoadAsync(isStudent: true);

    /// <summary>The id of the signed-in teacher's record in the teachers table, or null when the signed-in person is not a teacher.</summary>
    public Task<int?> GetTeacherIdAsync() => teacherLookup ??= LoadAsync(isStudent: false);

    private async Task<int?> LoadAsync(bool isStudent)
    {
        if (await GetAsync() is not { } me)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        return isStudent
            ? await db.Students.AsNoTracking().Where(s => s.UserId == me.UserId).Select(s => (int?)s.Id).FirstOrDefaultAsync()
            : await db.Teachers.AsNoTracking().Where(t => t.UserId == me.UserId).Select(t => (int?)t.Id).FirstOrDefaultAsync();
    }
}
