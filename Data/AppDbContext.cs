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

    // Classes and enrollment
    public DbSet<SchoolYear> SchoolYears => Set<SchoolYear>();
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Strand> Strands => Set<Strand>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<StrandSchedule> StrandSchedules => Set<StrandSchedule>();
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
    public DbSet<ModuleView> ModuleViews => Set<ModuleView>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    // Messages and notifications
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // The four staff tables share one configuration...
        modelBuilder.ApplyConfiguration(new StaffProfileConfiguration<Teacher>(u => u.Teacher));
        modelBuilder.ApplyConfiguration(new StaffProfileConfiguration<Registrar>(u => u.Registrar));
        modelBuilder.ApplyConfiguration(new StaffProfileConfiguration<GuidanceCounselor>(u => u.GuidanceCounselor));
        modelBuilder.ApplyConfiguration(new StaffProfileConfiguration<Administrator>(u => u.Administrator));

        // ...and three of them also have a contact number and a status.
        modelBuilder.ApplyConfiguration(new StaffContactConfiguration<Registrar>("registrars"));
        modelBuilder.ApplyConfiguration(new StaffContactConfiguration<GuidanceCounselor>("guidance_counselors"));
        modelBuilder.ApplyConfiguration(new StaffContactConfiguration<Administrator>("administrators"));
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
