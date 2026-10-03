using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data.Queries;

/// <summary>One line of the "Your account" card. A null value is shown as "Not set yet".</summary>
/// <param name="IsStatus">True for an Active/Inactive value, which the screen shows as a colored badge.</param>
public sealed record DetailRow(string Label, string? Value, bool IsStatus = false);

/// <summary>What the home page shows about the signed-in person: who they are and where they belong.</summary>
public sealed record AccountDetails(string FullName, string FirstName, string Email, string Role, IReadOnlyList<DetailRow> Rows)
{
    /// <summary>The details of one account, or null when the account no longer exists.</summary>
    public static async Task<AccountDetails?> LoadAsync(AppDbContext db, int userId, CancellationToken ct = default)
    {
        // The name lives in the profile table of the user's role, so load those too (only one of them has a row).
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.Administrator)
            .Include(u => u.GuidanceCounselor)
            .Include(u => u.Registrar)
            .Include(u => u.Teacher)
            .Include(u => u.Student)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return null;
        }

        var rows = new List<DetailRow>();
        switch (user.Profile)
        {
            case Student student:
                rows.Add(new("LRN", student.Lrn));
                await AddPlacementAsync(rows, db, student, ct);
                break;

            case Teacher teacher:
                rows.Add(new("Employee number", teacher.EmployeeNumber));
                await AddPlacementAsync(rows, db, teacher, ct);
                break;

            case StaffContactProfile staff:
                rows.Add(new("Employee number", staff.EmployeeNumber));
                rows.Add(new("Phone", staff.PhoneNumber));
                rows.Add(new("Status", staff.Status.ToString(), IsStatus: true));
                break;
        }

        // An account made by hand without a profile row still works, it just has no name yet.
        var firstName = user.Profile?.FirstName ?? user.Email;
        return new AccountDetails(user.DisplayName, firstName, user.Email, AppRoles.DisplayName(user.Role.Name), rows);
    }

    private static async Task AddPlacementAsync(List<DetailRow> rows, AppDbContext db, IHasPlacement person, CancellationToken ct)
    {
        var trackId = person.TrackId;
        var strandId = person.StrandId;
        var subjectId = person.SubjectId;
        var sectionId = person.SectionId;

        string? track = null;
        if (trackId is not null)
        {
            track = await db.Tracks.AsNoTracking().Where(t => t.Id == trackId).Select(t => t.Name).FirstOrDefaultAsync(ct);
        }

        string? strand = null;
        if (strandId is not null)
        {
            var found = await db.Strands.AsNoTracking().Where(s => s.Id == strandId).Select(s => new { s.Name, s.Code }).FirstOrDefaultAsync(ct);
            strand = found is null ? null : $"{found.Name} ({found.Code})";
        }

        string? subject = null;
        if (subjectId is not null)
        {
            subject = await db.Subjects.AsNoTracking().Where(s => s.Id == subjectId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        }

        string? section = null;
        if (sectionId is not null)
        {
            var found = await db.Sections.AsNoTracking().Where(s => s.Id == sectionId)
                .Select(s => new { s.GradeLevel, StrandCode = s.Strand.Code, s.Name })
                .FirstOrDefaultAsync(ct);
            section = found is null ? null : $"{found.GradeLevel}-{found.StrandCode}-{found.Name}";
        }

        rows.Add(new("Grade level", person.GradeLevel is { } grade ? $"Grade {grade}" : null));
        rows.Add(new("Track", track));
        rows.Add(new("Strand", strand));
        rows.Add(new("Subject", subject));
        rows.Add(new("Section", section));
    }
}
