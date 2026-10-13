using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data;

public class AppDbContext : DbContext
{
    private readonly TimeProvider _clock;

    public AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider? clock = null) : base(options)
    {
        _clock = clock ?? TimeProvider.System;
    }

    // Users and roles
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Registrar> Registrars => Set<Registrar>();
    public DbSet<GuidanceCounselor> GuidanceCounselors => Set<GuidanceCounselor>();
    public DbSet<Administrator> Administrators => Set<Administrator>();
    public DbSet<AcademicCoordinator> AcademicCoordinators => Set<AcademicCoordinator>();
    public DbSet<AcademicProvider> AcademicProviders => Set<AcademicProvider>();

    // Classes and enrollment
    public DbSet<SchoolYear> SchoolYears => Set<SchoolYear>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Strand> Strands => Set<Strand>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<CourseSchedule> CourseSchedules => Set<CourseSchedule>();
    public DbSet<StrandSubject> StrandSubjects => Set<StrandSubject>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<EnrollmentSubject> EnrollmentSubjects => Set<EnrollmentSubject>();

    // Subjects, modules, activities and grades
    public DbSet<TeachingAssignment> TeachingAssignments => Set<TeachingAssignment>();
    public DbSet<LearningModule> Modules => Set<LearningModule>();
    public DbSet<ModuleItem> ModuleItems => Set<ModuleItem>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<FinalGrade> FinalGrades => Set<FinalGrade>();
    public DbSet<ModuleView> ModuleViews => Set<ModuleView>();
    public DbSet<ItemView> ItemViews => Set<ItemView>();
    public DbSet<CalendarEntry> CalendarEntries => Set<CalendarEntry>();
    public DbSet<AchievementAward> AchievementAwards => Set<AchievementAward>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Referral> Referrals => Set<Referral>();

    // The academic coordinator: course materials and the teacher evaluation
    public DbSet<MaterialModule> MaterialModules => Set<MaterialModule>();
    public DbSet<MaterialItem> MaterialItems => Set<MaterialItem>();
    public DbSet<MaterialQuestion> MaterialQuestions => Set<MaterialQuestion>();
    public DbSet<MaterialDelivery> MaterialDeliveries => Set<MaterialDelivery>();
    public DbSet<TeacherEvaluation> TeacherEvaluations => Set<TeacherEvaluation>();
    public DbSet<EvaluationQuestion> EvaluationQuestions => Set<EvaluationQuestion>();
    public DbSet<EvaluationTarget> EvaluationTargets => Set<EvaluationTarget>();
    public DbSet<EvaluationResponse> EvaluationResponses => Set<EvaluationResponse>();
    public DbSet<EvaluationAnswer> EvaluationAnswers => Set<EvaluationAnswer>();

    // What people share: pictures (and, below, friends, groups and files)
    public DbSet<ProfilePhoto> ProfilePhotos => Set<ProfilePhoto>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<StudyGroup> StudyGroups => Set<StudyGroup>();
    public DbSet<StudyGroupMember> StudyGroupMembers => Set<StudyGroupMember>();
    public DbSet<GroupPost> GroupPosts => Set<GroupPost>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<StoredFileContent> StoredFileContents => Set<StoredFileContent>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AttachmentContent> AttachmentContents => Set<AttachmentContent>();
    public DbSet<SharedFile> SharedFiles => Set<SharedFile>();

    // The administrator: the security log and the system settings
    public DbSet<SecurityLog> SecurityLogs => Set<SecurityLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // Messages and notifications
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // The staff tables share one configuration...
        modelBuilder.ApplyConfiguration(new StaffProfileConfiguration<Teacher>(u => u.Teacher));
        modelBuilder.ApplyConfiguration(new StaffProfileConfiguration<Registrar>(u => u.Registrar));
        modelBuilder.ApplyConfiguration(new StaffProfileConfiguration<GuidanceCounselor>(u => u.GuidanceCounselor));
        modelBuilder.ApplyConfiguration(new StaffProfileConfiguration<Administrator>(u => u.Administrator));
        modelBuilder.ApplyConfiguration(new StaffProfileConfiguration<AcademicCoordinator>(u => u.AcademicCoordinator));
        modelBuilder.ApplyConfiguration(new StaffProfileConfiguration<AcademicProvider>(u => u.AcademicProvider));

        // ...and most of them also have a contact number and a status.
        modelBuilder.ApplyConfiguration(new StaffContactConfiguration<Registrar>("registrars"));
        modelBuilder.ApplyConfiguration(new StaffContactConfiguration<GuidanceCounselor>("guidance_counselors"));
        modelBuilder.ApplyConfiguration(new StaffContactConfiguration<Administrator>("administrators"));
        modelBuilder.ApplyConfiguration(new StaffContactConfiguration<AcademicCoordinator>("academic_coordinators"));
        modelBuilder.ApplyConfiguration(new StaffContactConfiguration<AcademicProvider>("academic_providers"));
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // MySQL returns DateTime with Kind=Unspecified. Everything we store is UTC, so say so on the way out.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditColumns();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampAuditColumns();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampAuditColumns()
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
                entry.Property(e => e.CreatedAtUtc).IsModified = false;
            }
        }
    }

    private sealed class UtcDateTimeConverter()
        : ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcDateTimeConverter()
        : ValueConverter<DateTime?, DateTime?>(
            v => v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
