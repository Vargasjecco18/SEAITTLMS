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
    private int? teacherId;
    private int? studentId;
    private bool teacherLoaded;
    private bool studentLoaded;

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

    /// <summary>The id of the signed-in student's record in the students table, or null when the signed-in person is not a student.</summary>
    public async Task<int?> GetStudentIdAsync()
    {
        if (!studentLoaded)
        {
            studentLoaded = true;
            if (await GetAsync() is { } me)
            {
                await using var db = await dbFactory.CreateDbContextAsync();
                studentId = await db.Students.AsNoTracking().Where(s => s.UserId == me.UserId).Select(s => (int?)s.Id).FirstOrDefaultAsync();
            }
        }

        return studentId;
    }

    /// <summary>The id of the signed-in teacher's record in the teachers table, or null when the signed-in person is not a teacher.</summary>
    public async Task<int?> GetTeacherIdAsync()
    {
        if (!teacherLoaded)
        {
            teacherLoaded = true;
            if (await GetAsync() is { } me)
            {
                await using var db = await dbFactory.CreateDbContextAsync();
                teacherId = await db.Teachers.AsNoTracking().Where(t => t.UserId == me.UserId).Select(t => (int?)t.Id).FirstOrDefaultAsync();
            }
        }

        return teacherId;
    }
}
