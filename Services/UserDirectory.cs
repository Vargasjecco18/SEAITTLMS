using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>A person as other people see them: their account, name and role.</summary>
public sealed record PersonRef(int UserId, string Name, string Role);

/// <summary>
/// Looks up the names of accounts. A person's name lives in the table of their role, so this asks each of the five tables.
/// </summary>
public static class UserDirectory
{
    public static string FullName(string first, string? middle, string last, string? extension = null) =>
        string.Join(' ', new[] { first, middle, last, extension }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public static async Task<Dictionary<int, PersonRef>> LoadAsync(AppDbContext db, IReadOnlyCollection<int> userIds, CancellationToken ct = default)
    {
        var result = new Dictionary<int, PersonRef>();
        if (userIds.Count == 0)
        {
            return result;
        }

        var ids = userIds.Distinct().ToList();

        foreach (var s in await db.Students.AsNoTracking().Where(x => ids.Contains(x.UserId)).Select(x => new { x.UserId, x.FirstName, x.MiddleName, x.LastName, x.NameExtension }).ToListAsync(ct))
        {
            result[s.UserId] = new(s.UserId, FullName(s.FirstName, s.MiddleName, s.LastName, s.NameExtension), AppRoles.Student);
        }

        foreach (var s in await db.Teachers.AsNoTracking().Where(x => ids.Contains(x.UserId)).Select(x => new { x.UserId, x.FirstName, x.MiddleName, x.LastName, x.NameExtension }).ToListAsync(ct))
        {
            result[s.UserId] = new(s.UserId, FullName(s.FirstName, s.MiddleName, s.LastName, s.NameExtension), AppRoles.Teacher);
        }

        foreach (var s in await db.Registrars.AsNoTracking().Where(x => ids.Contains(x.UserId)).Select(x => new { x.UserId, x.FirstName, x.MiddleName, x.LastName, x.NameExtension }).ToListAsync(ct))
        {
            result[s.UserId] = new(s.UserId, FullName(s.FirstName, s.MiddleName, s.LastName, s.NameExtension), AppRoles.Registrar);
        }

        foreach (var s in await db.GuidanceCounselors.AsNoTracking().Where(x => ids.Contains(x.UserId)).Select(x => new { x.UserId, x.FirstName, x.MiddleName, x.LastName, x.NameExtension }).ToListAsync(ct))
        {
            result[s.UserId] = new(s.UserId, FullName(s.FirstName, s.MiddleName, s.LastName, s.NameExtension), AppRoles.GuidanceCounselor);
        }

        foreach (var s in await db.Administrators.AsNoTracking().Where(x => ids.Contains(x.UserId)).Select(x => new { x.UserId, x.FirstName, x.MiddleName, x.LastName, x.NameExtension }).ToListAsync(ct))
        {
            result[s.UserId] = new(s.UserId, FullName(s.FirstName, s.MiddleName, s.LastName, s.NameExtension), AppRoles.Admin);
        }

        return result;
    }
}
