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

        // Lets class offerings and enrollments prove they use a term of the same school year as their section.
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

        // The four tracks of the DepEd senior high school curriculum. Switch off (is_active = 0) the ones SEAITT
        // doesn't offer, rather than deleting them.
        b.HasData(
            Seed(1, "ACAD", "Academic"),
            Seed(2, "TVL", "Technical-Vocational-Livelihood"),
            Seed(3, "SPORTS", "Sports"),
            Seed(4, "ARTS", "Arts and Design"));
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
    }
}

internal sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> b)
    {
        b.HasOne(s => s.SchoolYear).WithMany().HasForeignKey(s => s.SchoolYearId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Strand).WithMany().HasForeignKey(s => s.StrandId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Adviser).WithMany().HasForeignKey(s => s.AdviserTeacherId).OnDelete(DeleteBehavior.Restrict);

        b.Property(s => s.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(s => new { s.SchoolYearId, s.StrandId, s.GradeLevel, s.Name }).IsUnique();

        // Lets class offerings and enrollments prove they use a section of the same school year as their term.
        b.HasAlternateKey(s => new { s.Id, s.SchoolYearId });

        // Lets students and teachers prove that the strand and grade level they list are the section's own.
        b.HasAlternateKey(s => new { s.Id, s.StrandId, s.GradeLevel });

        b.ToTable(t => t.HasCheckConstraint("ck_sections_grade_level", "grade_level IN (11, 12)"));
    }
}

internal sealed class ClassOfferingConfiguration : IEntityTypeConfiguration<ClassOffering>
{
    public void Configure(EntityTypeBuilder<ClassOffering> b)
    {
        // (term, school_year) and (section, school_year) share school_year_id, so the term and the
        // section can only be combined when they belong to the same school year.
        b.HasOne(c => c.Term)
            .WithMany()
            .HasForeignKey(c => new { c.TermId, c.SchoolYearId })
            .HasPrincipalKey(t => new { t.Id, t.SchoolYearId })
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(c => c.Section)
            .WithMany()
            .HasForeignKey(c => new { c.SectionId, c.SchoolYearId })
            .HasPrincipalKey(s => new { s.Id, s.SchoolYearId })
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(c => c.Subject).WithMany().HasForeignKey(c => c.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(c => c.Teacher).WithMany().HasForeignKey(c => c.TeacherId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(c => new { c.TermId, c.SectionId, c.SubjectId }).IsUnique();
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

        // A student is in exactly one section per term.
        b.HasIndex(e => new { e.StudentId, e.TermId }).IsUnique();
    }
}
