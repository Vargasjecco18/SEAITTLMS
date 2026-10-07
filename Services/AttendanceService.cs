using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record AttendanceRow(int StudentId, string Name, string? Lrn, string? Status);

public sealed record AttendanceSheet(int AssignmentId, string SubjectName, string SectionLabel, DateOnly Date, bool Saved, IReadOnlyList<AttendanceRow> Rows);

public sealed record AttendanceSummaryRow(int StudentId, string Name, int Present, int Late, int Absent, int Days, decimal? Percent);

public sealed record AttendanceSummary(int AssignmentId, int DaysRecorded, IReadOnlyList<AttendanceSummaryRow> Rows, decimal? ClassPercent);

public sealed record SubjectAttendance(int AssignmentId, string SubjectName, int Present, int Late, int Absent, decimal? Percent);

public sealed record StudentAttendance(IReadOnlyList<SubjectAttendance> Subjects, int Present, int Late, int Absent, decimal? Percent)
{
    public int Days => Present + Late + Absent;
}

/// <summary>
/// Attendance: the teacher of a subject marks Present, Late or Absent for every student of the section, one day at a time. The
/// attendance rate is the days present or late divided by the days marked (a student who is late still came to class).
/// </summary>
public sealed class AttendanceService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    /// <summary>The attendance rate under which a student is "not attending class".</summary>
    public const decimal LowAttendancePercent = 80;

    public static decimal? Rate(int present, int late, int absent) => present + late + absent == 0 ? null : Math.Round((present + late) * 100m / (present + late + absent), 1);

    // ======================================================================= the teacher

    public async Task<AttendanceSheet?> GetSheetAsync(int teacherId, int assignmentId, DateOnly date, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var a = await OwnAssignmentAsync(db, teacherId, assignmentId, ct);
        if (a is null)
        {
            return null;
        }

        var students = await Roster.Enrollments(db, a.SectionId, a.SubjectId)
            .Select(e => new { e.StudentId, e.Student.FirstName, e.Student.LastName, e.Student.Lrn }).Distinct()
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToListAsync(ct);
        var marks = await db.AttendanceRecords.AsNoTracking().Where(r => r.AssignmentId == assignmentId && r.AttendedOn == date).ToDictionaryAsync(r => r.StudentId, r => r.Status, ct);

        return new AttendanceSheet(assignmentId, a.SubjectName, a.Label, date, marks.Count > 0,
            students.Select(s => new AttendanceRow(s.StudentId, $"{s.LastName}, {s.FirstName}", s.Lrn, marks.GetValueOrDefault(s.StudentId))).ToList());
    }

    /// <summary>
    /// Saves the marks of a day. <paramref name="statuses"/> maps a student to Present, Late or Absent; an empty value takes the mark away.
    /// Students that are not in the map are left as they are. A day that has not come yet can't be marked.
    /// </summary>
    public async Task<ServiceResult> SaveSheetAsync(int teacherId, int assignmentId, DateOnly date, IReadOnlyDictionary<int, string?> statuses, CancellationToken ct = default)
    {
        if (date > PhilippineTime.Today(clock))
        {
            return ServiceResult.Fail("You can't mark attendance for a day that has not come yet.");
        }

        if (date.Year < 2000)
        {
            return ServiceResult.Fail("Pick a real date.");
        }

        if (statuses.Values.Any(v => !string.IsNullOrEmpty(v) && !AttendanceStatus.All.Contains(v)))
        {
            return ServiceResult.Fail("Every student is Present, Late or Absent.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var a = await OwnAssignmentAsync(db, teacherId, assignmentId, ct);
        if (a is null)
        {
            return ServiceResult.Fail("This is not your course.");
        }

        var roster = await Roster.StudentIds(db, a.SectionId, a.SubjectId).ToListAsync(ct);
        var known = statuses.Where(s => roster.Contains(s.Key)).ToList();
        if (known.Count == 0)
        {
            return ServiceResult.Fail("Mark at least one student.");
        }

        var existing = await db.AttendanceRecords.Where(r => r.AssignmentId == assignmentId && r.AttendedOn == date).ToDictionaryAsync(r => r.StudentId, ct);
        foreach (var (studentId, status) in known)
        {
            existing.TryGetValue(studentId, out var record);
            if (string.IsNullOrEmpty(status))
            {
                if (record is not null)
                {
                    db.AttendanceRecords.Remove(record);
                }

                continue;
            }

            if (record is null)
            {
                db.AttendanceRecords.Add(new AttendanceRecord { AssignmentId = assignmentId, StudentId = studentId, AttendedOn = date, Status = status });
            }
            else
            {
                record.Status = status;
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("Somebody else saved this day at the same moment. Open it again.");
        }

        var all = await db.AttendanceRecords.AsNoTracking().Where(r => r.AssignmentId == assignmentId && r.AttendedOn == date).Select(r => r.Status).ToListAsync(ct);
        return ServiceResult.Ok($"Saved: {all.Count(s => s == AttendanceStatus.Present)} present, {all.Count(s => s == AttendanceStatus.Late)} late, {all.Count(s => s == AttendanceStatus.Absent)} absent.");
    }

    public async Task<AttendanceSummary?> GetSummaryAsync(int teacherId, int assignmentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var a = await OwnAssignmentAsync(db, teacherId, assignmentId, ct);
        if (a is null)
        {
            return null;
        }

        var students = await Roster.Enrollments(db, a.SectionId, a.SubjectId)
            .Select(e => new { e.StudentId, e.Student.FirstName, e.Student.LastName }).Distinct()
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToListAsync(ct);
        var records = await db.AttendanceRecords.AsNoTracking().Where(r => r.AssignmentId == assignmentId).Select(r => new { r.StudentId, r.Status, r.AttendedOn }).ToListAsync(ct);

        var rows = students.Select(s =>
        {
            var mine = records.Where(r => r.StudentId == s.StudentId).ToList();
            int present = mine.Count(r => r.Status == AttendanceStatus.Present), late = mine.Count(r => r.Status == AttendanceStatus.Late), absent = mine.Count(r => r.Status == AttendanceStatus.Absent);
            return new AttendanceSummaryRow(s.StudentId, $"{s.LastName}, {s.FirstName}", present, late, absent, present + late + absent, Rate(present, late, absent));
        }).ToList();

        var rated = rows.Where(r => r.Percent is not null).ToList();
        decimal? classRate = rated.Count == 0 ? null : Rate(rated.Sum(r => r.Present), rated.Sum(r => r.Late), rated.Sum(r => r.Absent));
        return new AttendanceSummary(assignmentId, records.Select(r => r.AttendedOn).Distinct().Count(), rows, classRate);
    }

    // ======================================================================= the student

    public async Task<StudentAttendance> GetStudentAsync(int studentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var assignments = await Roster.AssignmentsOf(db, studentId).AsNoTracking().OrderBy(a => a.Subject.Name)
            .Select(a => new { a.Id, Subject = a.Subject.Name }).ToListAsync(ct);
        var ids = assignments.Select(a => a.Id).ToList();
        var records = await db.AttendanceRecords.AsNoTracking().Where(r => r.StudentId == studentId && ids.Contains(r.AssignmentId)).Select(r => new { r.AssignmentId, r.Status }).ToListAsync(ct);

        var subjects = assignments.Select(a =>
        {
            var mine = records.Where(r => r.AssignmentId == a.Id).ToList();
            int present = mine.Count(r => r.Status == AttendanceStatus.Present), late = mine.Count(r => r.Status == AttendanceStatus.Late), absent = mine.Count(r => r.Status == AttendanceStatus.Absent);
            return new SubjectAttendance(a.Id, a.Subject, present, late, absent, Rate(present, late, absent));
        }).ToList();

        int p = subjects.Sum(s => s.Present), l = subjects.Sum(s => s.Late), ab = subjects.Sum(s => s.Absent);
        return new StudentAttendance(subjects, p, l, ab, Rate(p, l, ab));
    }

    // ======================================================================= helpers

    private sealed record Own(int Id, int SectionId, int SubjectId, string SubjectName, string Label);

    private static async Task<Own?> OwnAssignmentAsync(AppDbContext db, int teacherId, int assignmentId, CancellationToken ct)
    {
        var a = await db.TeachingAssignments.AsNoTracking().Where(x => x.Id == assignmentId && x.TeacherId == teacherId)
            .Select(x => new { x.Id, x.SectionId, x.SubjectId, Subject = x.Subject.Name, x.Section.GradeLevel, Strand = x.Section.Strand.Code, Section = x.Section.Name })
            .FirstOrDefaultAsync(ct);
        return a is null ? null : new Own(a.Id, a.SectionId, a.SubjectId, a.Subject, TeachingService.SectionLabel(a.GradeLevel, a.Strand, a.Section));
    }
}
