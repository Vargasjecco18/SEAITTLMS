using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data;

internal sealed class SchoolYearConfiguration : IEntityTypeConfiguration<SchoolYear>
{
    public void Configure(EntityTypeBuilder<SchoolYear> b)
    {
        b.Property(s => s.Name).HasMaxLength(20).IsRequired();
        b.HasIndex(s => s.Name).IsUnique();

        b.ToTable(t => t.HasCheckConstraint("ck_school_years_dates", "end_date > start_date"));
    }
}

internal sealed class TermConfiguration : IEntityTypeConfiguration<Term>
{
    public void Configure(EntityTypeBuilder<Term> b)
    {
        b.HasOne(t => t.SchoolYear)
            .WithMany(s => s.Terms)
            .HasForeignKey(t => t.SchoolYearId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Property(t => t.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(t => new { t.SchoolYearId, t.TermNumber }).IsUnique();

        // Lets enrollments prove they use a term of the same school year as their section.
        b.HasAlternateKey(t => new { t.Id, t.SchoolYearId });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_terms_term_number", "term_number >= 1");
            t.HasCheckConstraint("ck_terms_dates", "end_date >= start_date");
        });
    }
}

internal sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    // HasData can't use the "now" that SaveChanges stamps, so the seed rows carry a fixed date.
    private static readonly DateTime SeedDate = new(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Track> b)
    {
        b.Property(t => t.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(t => t.Code).IsUnique();
        b.Property(t => t.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(t => t.Name).IsUnique();

        // The two tracks SEAITT offers. (An earlier version also had TVL, SPORTS and ARTS: TVL became TECH-PRO.)
        b.HasData(
            Seed(1, "ACAD", "Academic"),
            Seed(2, "TECH-PRO", "Technical-Professional"));
    }

    private static Track Seed(int id, string code, string name) => new()
    {
        Id = id,
        Code = code,
        Name = name,
        IsActive = true,
        CreatedAtUtc = SeedDate,
        UpdatedAtUtc = SeedDate,
    };
}

internal sealed class StrandConfiguration : IEntityTypeConfiguration<Strand>
{
    public void Configure(EntityTypeBuilder<Strand> b)
    {
        b.Property(s => s.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(s => s.Code).IsUnique();
        b.Property(s => s.Name).HasMaxLength(150).IsRequired();

        b.HasOne(s => s.Track).WithMany().HasForeignKey(s => s.TrackId).OnDelete(DeleteBehavior.Restrict);

        // Lets students and teachers prove that the track they list is the track of the strand they list.
        b.HasAlternateKey(s => new { s.Id, s.TrackId });
    }
}

internal sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> b)
    {
        b.Property(s => s.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(s => s.Code).IsUnique();
        b.Property(s => s.Name).HasMaxLength(150).IsRequired();
        b.Property(s => s.Description).HasMaxLength(500);
        b.Property(s => s.Units).HasColumnType("decimal(3,1)");
        b.ToTable(t => t.HasCheckConstraint("ck_subjects_units", "units >= 0.5 AND units <= 12"));
    }
}

internal sealed class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> b)
    {
        b.Property(s => s.Code).HasMaxLength(10).IsRequired();
        b.HasIndex(s => s.Code).IsUnique();
        b.Property(s => s.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(s => s.Name).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("ck_schedules_hours", "end_time > start_time"));
    }
}

internal sealed class CourseScheduleConfiguration : IEntityTypeConfiguration<CourseSchedule>
{
    public void Configure(EntityTypeBuilder<CourseSchedule> b)
    {
        b.ToTable("course_schedules");
        b.Property(c => c.Days).HasMaxLength(3).IsRequired();

        // Gone with the section; a course that has class times can't be deleted until they are cleared.
        b.HasOne(c => c.Section).WithMany().HasForeignKey(c => c.SectionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(c => c.Subject).WithMany().HasForeignKey(c => c.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(c => c.Schedule).WithMany().HasForeignKey(c => c.ScheduleId).OnDelete(DeleteBehavior.Restrict);

        // A course of a section meets at one time.
        b.HasIndex(c => new { c.SectionId, c.SubjectId }).IsUnique();

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_course_schedules_days", "days IN ('MWF', 'TTH')");
            t.HasCheckConstraint("ck_course_schedules_hours", "end_time > start_time");
        });
    }
}

internal sealed class StrandSubjectConfiguration : IEntityTypeConfiguration<StrandSubject>
{
    public void Configure(EntityTypeBuilder<StrandSubject> b)
    {
        b.HasKey(x => new { x.StrandId, x.GradeLevel, x.TermNumber, x.SubjectId });
        b.HasOne(x => x.Strand).WithMany().HasForeignKey(x => x.StrandId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_strand_subjects_grade_level", "grade_level IN (11, 12)");
            t.HasCheckConstraint("ck_strand_subjects_term_number", "term_number >= 1");
        });
    }
}

internal sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> b)
    {
        b.HasOne(s => s.SchoolYear).WithMany().HasForeignKey(s => s.SchoolYearId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Strand).WithMany().HasForeignKey(s => s.StrandId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Adviser).WithMany().HasForeignKey(s => s.AdviserTeacherId).OnDelete(DeleteBehavior.Restrict);

        // A section is the school year, the strand, the grade level and the name: 11-BUSSENT-A.
        b.Property(s => s.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(s => new { s.SchoolYearId, s.StrandId, s.GradeLevel, s.Name }).IsUnique();

        // Lets enrollments prove they use a section of the same school year as their term.
        b.HasAlternateKey(s => new { s.Id, s.SchoolYearId });

        // Lets students and teachers prove that the strand and grade level they list are the section's own.
        b.HasAlternateKey(s => new { s.Id, s.StrandId, s.GradeLevel });

        b.ToTable(t => t.HasCheckConstraint("ck_sections_grade_level", "grade_level IN (11, 12)"));
    }
}

internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.HasOne(e => e.Term)
            .WithMany()
            .HasForeignKey(e => new { e.TermId, e.SchoolYearId })
            .HasPrincipalKey(t => new { t.Id, t.SchoolYearId })
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(e => e.Section)
            .WithMany()
            .HasForeignKey(e => new { e.SectionId, e.SchoolYearId })
            .HasPrincipalKey(s => new { s.Id, s.SchoolYearId })
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(e => e.Student).WithMany().HasForeignKey(e => e.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(e => e.EnrolledBy).WithMany().HasForeignKey(e => e.EnrolledByUserId).OnDelete(DeleteBehavior.SetNull);

        b.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // The student as they were when enrolled: who, and where.
        b.Property(e => e.Lrn).HasMaxLength(12).IsFixedLength().IsRequired();
        b.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
        b.Property(e => e.MiddleName).HasMaxLength(100);
        b.Property(e => e.LastName).HasMaxLength(100).IsRequired();
        b.Property(e => e.NameExtension).HasMaxLength(10);
        b.Property(e => e.Subjects).HasMaxLength(1000);

        b.HasOne(e => e.Track).WithMany().HasForeignKey(e => e.TrackId).OnDelete(DeleteBehavior.Restrict);

        // The strand must belong to the track, and the section must be the section of that strand and grade level.
        b.HasOne(e => e.Strand)
            .WithMany()
            .HasForeignKey(e => new { e.StrandId, e.TrackId })
            .HasPrincipalKey(s => new { s.Id, s.TrackId })
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne<Section>()
            .WithMany()
            .HasForeignKey(e => new { e.SectionId, e.StrandId, e.GradeLevel })
            .HasPrincipalKey(s => new { s.Id, s.StrandId, s.GradeLevel })
            .OnDelete(DeleteBehavior.Restrict);

        // A student is in exactly one section per term.
        b.HasIndex(e => new { e.StudentId, e.TermId }).IsUnique();

        b.ToTable(t => t.HasCheckConstraint("ck_enrollments_grade_level", "grade_level IN (11, 12)"));
    }
}

internal sealed class EnrollmentSubjectConfiguration : IEntityTypeConfiguration<EnrollmentSubject>
{
    public void Configure(EntityTypeBuilder<EnrollmentSubject> b)
    {
        b.HasKey(x => new { x.EnrollmentId, x.SubjectId });

        // Removing an enrollment removes its subject rows with it.
        b.HasOne(x => x.Enrollment).WithMany(e => e.EnrolledSubjects).HasForeignKey(x => x.EnrollmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
