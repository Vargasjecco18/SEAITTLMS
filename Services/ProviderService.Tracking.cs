using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>A syllabus or a module that was sent to a teacher's course and that the teacher has not posted for the students (not opened, a draft, or scheduled for later).</summary>
public sealed record NotPostedRow(
    int DeliveryId, int TeacherUserId, string TeacherName, string SectionLabel, int SubjectId, string SubjectName, string Title, bool IsSyllabus, DateTime SentAtUtc, string State, DateTime? ScheduledForUtc);

/// <summary>A student who has not opened a syllabus or a module that the teacher posted.</summary>
public sealed record NotOpenedRow(
    int StudentId, int StudentUserId, string StudentName, string SectionLabel, int TeacherUserId, string TeacherName, int SubjectId, string SubjectName, string Title, bool IsSyllabus, DateTime PostedAtUtc);

/// <summary>One student and one summative exam: did the student open it, hand it in, and what was the score.</summary>
public sealed record ExamStudentRow(
    int ItemId, int StudentId, int StudentUserId, string StudentName, string SectionLabel, int GradeLevel, int SubjectId, string SubjectName, string ExamTitle,
    DateTime? OpensAtUtc, DateTime? DueAtUtc, bool Opened, bool HandedIn, decimal? Score, decimal MaxScore)
{
    public int? Percent => HandedIn && Score is { } score && MaxScore > 0 ? (int)Math.Round(score / MaxScore * 100, MidpointRounding.AwayFromZero) : null;
}

public sealed record TrackPage<T>(IReadOnlyList<T> Rows, int Total, int Page, int PageSize);

/// <summary>What the tracking page is looking at: the teachers who did not post, the students who did not open, or the students of an exam.</summary>
public static class TrackStates
{
    public const string NotPosted = "NotPosted";
    public const string NotOpened = "NotOpened";
    public const string NotHandedIn = "NotHandedIn";

    /// <summary>Modules and syllabi: the teacher has set a time for the students to see them. Exams: the students who handed the exam in.</summary>
    public const string Scheduled = "Scheduled";
    public const string HandedIn = "HandedIn";
    public const string All = "All";
}

public sealed partial class ProviderService
{
    private static bool Matches(string? search, params string?[] texts)
    {
        var tokens = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.All(t => texts.Any(x => x?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static TrackPage<T> Page<T>(IReadOnlyList<T> all, int page, int size)
    {
        size = Math.Clamp(size, 5, 100);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(all.Count / (double)size)));
        return new TrackPage<T>(all.Skip((page - 1) * size).Take(size).ToList(), all.Count, page, size);
    }

    // ======================================================================= the teachers who did not post

    /// <summary>
    /// The syllabi (or the modules) that were sent and that the teacher has not posted yet and has not scheduled either (the teacher has not opened them, or has them as a draft). A module
    /// scheduled for a time that has come is posted (the clock decides, not a switch), so it is not here; one scheduled for later is in <see cref="GetScheduledAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<NotPostedRow>> GetNotPostedAsync(bool syllabus, string? search = null, CancellationToken ct = default) =>
        await NotPostedRowsAsync(syllabus, search, scheduled: false, ct);

    /// <summary>The syllabi (or the modules) that the teacher has scheduled: the students will see them at the time the teacher chose.</summary>
    public async Task<IReadOnlyList<NotPostedRow>> GetScheduledAsync(bool syllabus, string? search = null, CancellationToken ct = default) =>
        await NotPostedRowsAsync(syllabus, search, scheduled: true, ct);

    private async Task<IReadOnlyList<NotPostedRow>> NotPostedRowsAsync(bool syllabus, string? search, bool scheduled, CancellationToken ct)
    {
        var snap = await LoadAsync(ct: ct);
        return snap.Deliveries
            .Where(d => d.IsSyllabus == syllabus && !d.IsPosted(snap.Now) && (d.State(snap.Now) == DeliveryStates.Scheduled) == scheduled)
            .Where(d => Matches(search, d.SubjectName, d.Title, d.TeacherName, d.SectionLabel))
            .OrderBy(d => scheduled ? d.VisibleFromUtc : d.SentAtUtc)
            .Select(d => new NotPostedRow(d.Id, d.TeacherUserId, d.TeacherName, d.SectionLabel, d.SubjectId, d.SubjectName, d.Title, d.IsSyllabus, d.SentAtUtc, d.State(snap.Now), d.VisibleFromUtc))
            .ToList();
    }

    public async Task<TrackPage<NotPostedRow>> ListNotPostedAsync(bool syllabus, string? search, int page, CancellationToken ct = default) =>
        Page(await GetNotPostedAsync(syllabus, search, ct), page, PageSize);

    public async Task<TrackPage<NotPostedRow>> ListScheduledAsync(bool syllabus, string? search, int page, CancellationToken ct = default) =>
        Page(await GetScheduledAsync(syllabus, search, ct), page, PageSize);

    // ======================================================================= the students who did not open

    /// <summary>The students who have not opened a syllabus (or a module) that the teacher posted for them.</summary>
    public async Task<IReadOnlyList<NotOpenedRow>> GetNotOpenedAsync(bool syllabus, string? search = null, CancellationToken ct = default)
    {
        var snap = await LoadAsync(ct: ct);
        var posted = snap.Deliveries.Where(d => d.IsSyllabus == syllabus && d.IsPosted(snap.Now)).ToList();
        if (posted.Count == 0)
        {
            return [];
        }

        var studentIds = posted.SelectMany(snap.StudentsOf).Distinct().ToList();
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var names = (await db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.UserId, s.FirstName, s.MiddleName, s.LastName, s.NameExtension }).ToListAsync(ct))
            .ToDictionary(s => s.Id, s => (s.UserId, Name: UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName, s.NameExtension)));

        var rows = new List<NotOpenedRow>();
        foreach (var d in posted)
        {
            foreach (var studentId in snap.StudentsOf(d).Where(s => !snap.ModuleViews.ContainsKey((d.ModuleId, s))))
            {
                if (!names.TryGetValue(studentId, out var who))
                {
                    continue;
                }

                rows.Add(new NotOpenedRow(studentId, who.UserId, who.Name, d.SectionLabel, d.TeacherUserId, d.TeacherName, d.SubjectId, d.SubjectName, d.Title, d.IsSyllabus, d.VisibleFromUtc ?? d.SentAtUtc));
            }
        }

        return [.. rows
            .Where(r => Matches(search, r.StudentName, r.SubjectName, r.Title, r.TeacherName, r.SectionLabel))
            .OrderBy(r => r.PostedAtUtc).ThenBy(r => r.SectionLabel, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.StudentName, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<TrackPage<NotOpenedRow>> ListNotOpenedAsync(bool syllabus, string? search, int page, CancellationToken ct = default) =>
        Page(await GetNotOpenedAsync(syllabus, search, ct), page, PageSize);

    // ======================================================================= the students of the summative exams

    /// <summary>
    /// The students of the summative exams that are posted (an exam that opens later is not posted yet: nobody of it is listed), with whether they opened it, handed it in and the score.
    /// <paramref name="state"/> is <see cref="TrackStates.NotOpened"/>, <see cref="TrackStates.NotHandedIn"/> or <see cref="TrackStates.HandedIn"/> to keep only those, or empty for everybody.
    /// Grade level 0 and course 0 mean all.
    /// </summary>
    public async Task<IReadOnlyList<ExamStudentRow>> GetExamStudentsAsync(string? state = null, int gradeLevel = 0, int subjectId = 0, string? search = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var exams = await db.ModuleItems.AsNoTracking()
            .Where(i => i.Kind == ItemKinds.Exam && i.MaterialItemId != null)
            .Select(i => new
            {
                i.Id, i.Title, i.VisibleFromUtc, i.DueAtUtc, i.MaxScore, i.Module.Assignment.SectionId, i.Module.Assignment.SubjectId, SubjectName = i.Module.Assignment.Subject.Name,
                i.Module.Assignment.Section.GradeLevel, StrandCode = i.Module.Assignment.Section.Strand.Code, SectionName = i.Module.Assignment.Section.Name,
            })
            .ToListAsync(ct);
        if (exams.Count == 0)
        {
            return [];
        }

        var itemIds = exams.Select(e => e.Id).ToList();
        var opened = (await db.ItemViews.AsNoTracking().Where(v => itemIds.Contains(v.ItemId)).Select(v => new { v.ItemId, v.StudentId }).ToListAsync(ct)).Select(v => (v.ItemId, v.StudentId)).ToHashSet();
        var handed = (await db.Submissions.AsNoTracking().Where(s => itemIds.Contains(s.ItemId) && s.Status != SubmissionStatus.InProgress)
                .Select(s => new { s.ItemId, s.StudentId, s.Score }).ToListAsync(ct))
            .GroupBy(s => (s.ItemId, s.StudentId)).ToDictionary(g => g.Key, g => g.First().Score);
        var roster = (await db.EnrollmentSubjects.AsNoTracking().Where(x => x.Enrollment.Status == EnrollmentStatus.Enrolled)
                .Select(x => new { x.Enrollment.SectionId, x.SubjectId, x.Enrollment.StudentId }).Distinct().ToListAsync(ct))
            .GroupBy(x => (x.SectionId, x.SubjectId)).ToDictionary(g => g.Key, g => g.Select(x => x.StudentId).ToList());
        var studentIds = roster.Values.SelectMany(x => x).Distinct().ToList();
        var names = (await db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.UserId, s.FirstName, s.MiddleName, s.LastName, s.NameExtension }).ToListAsync(ct))
            .ToDictionary(s => s.Id, s => (s.UserId, Name: UserDirectory.FullName(s.FirstName, s.MiddleName, s.LastName, s.NameExtension)));

        var rows = new List<ExamStudentRow>();
        foreach (var e in exams)
        {
            if (gradeLevel > 0 && e.GradeLevel != gradeLevel || subjectId > 0 && e.SubjectId != subjectId)
            {
                continue;
            }

            // An exam that opens later is not posted yet: its students are not listed.
            if (e.VisibleFromUtc is { } opensAt && opensAt > now)
            {
                continue;
            }

            foreach (var studentId in roster.GetValueOrDefault((e.SectionId, e.SubjectId)) ?? [])
            {
                if (!names.TryGetValue(studentId, out var who))
                {
                    continue;
                }

                var didOpen = opened.Contains((e.Id, studentId)) || handed.ContainsKey((e.Id, studentId));
                var didHandIn = handed.ContainsKey((e.Id, studentId));
                var row = new ExamStudentRow(
                    e.Id, studentId, who.UserId, who.Name, TeachingService.SectionLabel(e.GradeLevel, e.StrandCode, e.SectionName), e.GradeLevel, e.SubjectId, e.SubjectName, e.Title,
                    e.VisibleFromUtc, e.DueAtUtc, didOpen, didHandIn, didHandIn ? handed[(e.Id, studentId)] : null, e.MaxScore);
                if (state == TrackStates.NotOpened && didOpen || state == TrackStates.NotHandedIn && didHandIn || state == TrackStates.HandedIn && !didHandIn)
                {
                    continue;
                }

                if (Matches(search, row.StudentName, row.SectionLabel, row.SubjectName, row.ExamTitle))
                {
                    rows.Add(row);
                }
            }
        }

        return [.. rows.OrderBy(r => r.SubjectName, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.ExamTitle, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.SectionLabel, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.StudentName, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<TrackPage<ExamStudentRow>> ListExamStudentsAsync(string? state, int gradeLevel, int subjectId, string? search, int page, CancellationToken ct = default) =>
        Page(await GetExamStudentsAsync(state, gradeLevel, subjectId, search, ct), page, PageSize);

    /// <summary>The courses and grade levels that have a summative exam posted, for the filters of the exam list.</summary>
    public async Task<(IReadOnlyList<int> Grades, IReadOnlyList<(int SubjectId, string Name)> Courses)> GetExamFilterChoicesAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.ModuleItems.AsNoTracking().Where(i => i.Kind == ItemKinds.Exam && i.MaterialItemId != null)
            .Select(i => new { i.Module.Assignment.SubjectId, Name = i.Module.Assignment.Subject.Name, i.Module.Assignment.Section.GradeLevel }).Distinct().ToListAsync(ct);
        return (rows.Select(r => (int)r.GradeLevel).Distinct().Order().ToList(), rows.GroupBy(r => r.SubjectId).Select(g => (g.Key, g.First().Name)).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    // ======================================================================= the message to everybody on the list

    /// <summary>
    /// Tells everybody who is on the list that is shown with a notification: the teachers who did not post (<paramref name="state"/> = <see cref="TrackStates.NotPosted"/>), the students who did not open
    /// a syllabus or a module (<see cref="TrackStates.NotOpened"/>) or, for the exams (<paramref name="exams"/>), the students who did not open or did not hand in. Each person gets one
    /// notification, whatever the number of lines they are on. Returns how many were told.
    /// </summary>
    public async Task<int> NotifyAsync(string state, bool exams, bool syllabus, string? search, int gradeLevel, int subjectId, string? message, CancellationToken ct = default)
    {
        if (notifications is null)
        {
            return 0;
        }

        List<int> users;
        string title;
        string link;
        string body;
        if (exams)
        {
            // Only the students who are late are told: the ones who did not open or did not hand in. Nobody is told about an exam they handed in.
            var rows = state is TrackStates.NotOpened or TrackStates.NotHandedIn ? await GetExamStudentsAsync(state, gradeLevel, subjectId, search, ct) : [];
            users = rows.Select(r => r.StudentUserId).Distinct().ToList();
            title = state == TrackStates.NotHandedIn ? "You have not handed in your summative exam" : "You have not opened your summative exam";
            body = string.IsNullOrWhiteSpace(message) ? "Open your course, find the summative exam and take it before it closes." : message.Trim();
            link = "student/subjects";
        }
        else if (state == TrackStates.Scheduled)
        {
            // What the teacher has scheduled is on its way: nobody has to be reminded.
            return 0;
        }
        else if (state == TrackStates.NotPosted)
        {
            var rows = await GetNotPostedAsync(syllabus, search, ct);
            users = rows.Select(r => r.TeacherUserId).Distinct().ToList();
            var what = syllabus ? "syllabus" : "modules";
            title = $"Please post the {what} for your students";
            body = string.IsNullOrWhiteSpace(message) ? $"The {what} that the academic provider sent you is not posted yet. Open your course and post it." : message.Trim();
            link = "teacher/subjects";
        }
        else
        {
            var rows = await GetNotOpenedAsync(syllabus, search, ct);
            users = rows.Select(r => r.StudentUserId).Distinct().ToList();
            var what = syllabus ? "syllabus" : "module";
            title = $"You have not opened the {what} yet";
            body = string.IsNullOrWhiteSpace(message) ? $"Your teacher posted a {what} for you. Open your course and read it." : message.Trim();
            link = "student/subjects";
        }

        if (users.Count == 0)
        {
            return 0;
        }

        await notifications.AddForManyAsync(users, "reminder", title, body, link, ct);
        return users.Count;
    }
}
