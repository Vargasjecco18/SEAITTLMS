using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Name).HasMaxLength(30).IsRequired();
        b.HasIndex(r => r.Name).IsUnique();

        // The roles are fixed, the code refers to them by these ids.
        b.HasData(AppRoles.Ids
            .OrderBy(kv => kv.Value)
            .Select(kv => new Role { Id = kv.Value, Name = kv.Key }));
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.Email).HasMaxLength(254).IsRequired();
        b.HasIndex(u => u.Email).IsUnique();

        b.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
        b.Property(u => u.PhoneNumber).HasMaxLength(20);

        b.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
        b.Property(u => u.ActiveSessionId).HasMaxLength(36);
        b.Property(u => u.NotificationSoundChoice).HasMaxLength(16);
        b.Ignore(u => u.Profile);
        b.Ignore(u => u.DisplayName);

        b.HasOne(u => u.Role)
            .WithMany()
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint("ck_users_failed_login_count", "failed_login_count >= 0"));
    }
}

/// <summary>The columns every role table has: who the person is.</summary>
internal static class PersonColumns
{
    public static void Configure<T>(EntityTypeBuilder<T> b) where T : PersonProfile
    {
        b.HasIndex(p => p.UserId).IsUnique();

        b.Property(p => p.FirstName).HasMaxLength(100).IsRequired();
        b.Property(p => p.MiddleName).HasMaxLength(100);
        b.Property(p => p.LastName).HasMaxLength(100).IsRequired();
        b.Property(p => p.NameExtension).HasMaxLength(10);
        b.Ignore(p => p.FullName);
    }
}

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> b)
    {
        b.HasOne(s => s.User)
            .WithOne(u => u.Student)
            .HasForeignKey<Student>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        PersonColumns.Configure(b);

        b.Property(s => s.Lrn).HasMaxLength(12).IsFixedLength().IsRequired();
        b.HasIndex(s => s.Lrn).IsUnique();

        b.Property(s => s.Sex).HasConversion<string>().HasMaxLength(10);

        b.Property(s => s.Status).HasMaxLength(10).HasDefaultValue(StudentStatus.Active).IsRequired();
        b.Property(s => s.StatusNote).HasMaxLength(300);
        b.HasIndex(s => s.Status);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_students_lrn", "lrn REGEXP '^[0-9]{12}$'");
            t.HasCheckConstraint("ck_students_status", "status IN ('Active', 'Graduated', 'DroppedOut')");
        });

        PlacementConfiguration.Configure(b, "students");
    }
}

internal sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> b) => PlacementConfiguration.Configure(b, "teachers");
}

/// <summary>
/// Grade level, track, strand, subject and section on students and teachers. All optional, but whatever is filled in
/// must agree: composite foreign keys tie (strand, track) to strands and (section, strand, grade level) to sections,
/// and the checks below say that a strand needs its track and a section needs its strand and grade level.
/// </summary>
internal static class PlacementConfiguration
{
    public static void Configure<T>(EntityTypeBuilder<T> b, string table) where T : class, IHasPlacement
    {
        b.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Track>().WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Strand>()
            .WithMany()
            .HasForeignKey(x => new { x.StrandId, x.TrackId })
            .HasPrincipalKey(s => new { s.Id, s.TrackId })
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Section>()
            .WithMany()
            .HasForeignKey(x => new { x.SectionId, x.StrandId, x.GradeLevel })
            .HasPrincipalKey(s => new { s.Id, s.StrandId, s.GradeLevel })
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t =>
        {
            t.HasCheckConstraint($"ck_{table}_grade_level", "grade_level IS NULL OR grade_level IN (11, 12)");
            t.HasCheckConstraint($"ck_{table}_strand_needs_track", "strand_id IS NULL OR track_id IS NOT NULL");
            t.HasCheckConstraint(
                $"ck_{table}_section_needs_strand_and_grade",
                "section_id IS NULL OR (strand_id IS NOT NULL AND grade_level IS NOT NULL)");
        });
    }
}

/// <summary>Teachers, registrars, guidance counselors and administrators: user link, name and employee number.</summary>
internal sealed class StaffProfileConfiguration<T>(Expression<Func<User, T?>> userNavigation) : IEntityTypeConfiguration<T>
    where T : StaffProfile
{
    public void Configure(EntityTypeBuilder<T> b)
    {
        b.HasOne(p => p.User)
            .WithOne(userNavigation)
            .HasForeignKey<T>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        PersonColumns.Configure(b);

        b.Property(p => p.EmployeeNumber).HasMaxLength(30);
        b.HasIndex(p => p.EmployeeNumber).IsUnique();
    }
}

/// <summary>Registrars, guidance counselors and administrators: contact number and status.</summary>
internal sealed class StaffContactConfiguration<T>(string table) : IEntityTypeConfiguration<T>
    where T : StaffContactProfile
{
    public void Configure(EntityTypeBuilder<T> b)
    {
        b.Property(p => p.PhoneNumber).HasMaxLength(20);
        b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        b.ToTable(t => t.HasCheckConstraint($"ck_{table}_status", "status IN ('Active', 'Inactive')"));
    }
}
