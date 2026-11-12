using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data;

internal sealed class StudentApplicationConfiguration : IEntityTypeConfiguration<StudentApplication>
{
    public void Configure(EntityTypeBuilder<StudentApplication> b)
    {
        b.ToTable("student_applications");
        b.Property(a => a.Reference).HasMaxLength(20).IsRequired();
        b.HasIndex(a => a.Reference).IsUnique();
        b.Property(a => a.FirstName).HasMaxLength(100).IsRequired();
        b.Property(a => a.MiddleName).HasMaxLength(100);
        b.Property(a => a.LastName).HasMaxLength(100).IsRequired();
        b.Property(a => a.Suffix).HasMaxLength(20);
        b.Property(a => a.Sex).HasMaxLength(10).IsRequired();
        b.Property(a => a.CivilStatus).HasMaxLength(30).IsRequired();
        b.Property(a => a.Nationality).HasMaxLength(60).IsRequired();
        b.Property(a => a.Religion).HasMaxLength(80).IsRequired();
        b.Property(a => a.Lrn).HasMaxLength(12);
        b.Property(a => a.Email).HasMaxLength(254).IsRequired();
        b.Property(a => a.MobileNumber).HasMaxLength(20).IsRequired();
        b.Property(a => a.Address).HasMaxLength(250).IsRequired();
        b.Property(a => a.Province).HasMaxLength(80).IsRequired();
        b.Property(a => a.City).HasMaxLength(80).IsRequired();
        b.Property(a => a.Barangay).HasMaxLength(80).IsRequired();
        b.Property(a => a.ZipCode).HasMaxLength(10).IsRequired();
        b.Property(a => a.AcademicYear).HasMaxLength(20).IsRequired();
        b.Property(a => a.FatherName).HasMaxLength(150);
        b.Property(a => a.FatherContact).HasMaxLength(20);
        b.Property(a => a.MotherName).HasMaxLength(150);
        b.Property(a => a.MotherContact).HasMaxLength(20);
        b.Property(a => a.GuardianName).HasMaxLength(150).IsRequired();
        b.Property(a => a.GuardianRelationship).HasMaxLength(50).IsRequired();
        b.Property(a => a.GuardianContact).HasMaxLength(20).IsRequired();
        b.Property(a => a.GuardianAddress).HasMaxLength(250).IsRequired();
        b.Property(a => a.Status).HasMaxLength(20).IsRequired();
        b.Property(a => a.DecisionNote).HasMaxLength(500);
        b.Ignore(a => a.FullName);

        b.HasOne(a => a.Track).WithMany().HasForeignKey(a => a.TrackId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.Strand).WithMany().HasForeignKey(a => a.StrandId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(a => a.DecidedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Student>().WithMany().HasForeignKey(a => a.EnrolledStudentId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(a => a.Status);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_student_applications_status", "status IN ('Pending', 'Accepted', 'Declined', 'Enrolled')");
            t.HasCheckConstraint("ck_student_applications_grade", "grade_level = 11");
        });
    }
}

internal sealed class ApplicationFileConfiguration : IEntityTypeConfiguration<ApplicationFile>
{
    public void Configure(EntityTypeBuilder<ApplicationFile> b)
    {
        b.ToTable("application_files");
        b.Property(f => f.Kind).HasMaxLength(20).IsRequired();
        b.Property(f => f.Name).HasMaxLength(200).IsRequired();
        b.Property(f => f.ContentType).HasMaxLength(100).IsRequired();
        b.Property(f => f.Data).HasColumnType("longblob");
        b.HasOne(f => f.Application).WithMany(a => a.Files).HasForeignKey(f => f.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(f => new { f.ApplicationId, f.Kind }).IsUnique();
    }
}

internal sealed class AdmissionOfficerConfiguration : IEntityTypeConfiguration<AdmissionOfficer>
{
    public void Configure(EntityTypeBuilder<AdmissionOfficer> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_admission_officers_grade", "grade_level IN (11, 12)"));
    }
}
