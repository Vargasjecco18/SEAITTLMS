using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data.Configurations;

internal sealed class AdmissionRecordConfiguration : IEntityTypeConfiguration<AdmissionRecord>
{
    public void Configure(EntityTypeBuilder<AdmissionRecord> b)
    {
        b.ToTable("admission_records");
        b.Property(r => r.Lrn).HasMaxLength(12).IsRequired();
        b.Property(r => r.FirstName).HasMaxLength(100).IsRequired();
        b.Property(r => r.MiddleName).HasMaxLength(100);
        b.Property(r => r.LastName).HasMaxLength(100).IsRequired();
        b.Property(r => r.NameExtension).HasMaxLength(10);
        b.Property(r => r.Sex).HasConversion<string>().HasMaxLength(10);
        b.Property(r => r.PhoneNumber).HasMaxLength(20);
        b.Property(r => r.Address).HasMaxLength(300);
        b.Property(r => r.PreviousSchool).HasMaxLength(200);
        b.Property(r => r.FatherName).HasMaxLength(150);
        b.Property(r => r.FatherPhone).HasMaxLength(20);
        b.Property(r => r.MotherName).HasMaxLength(150);
        b.Property(r => r.MotherPhone).HasMaxLength(20);
        b.Property(r => r.GuardianName).HasMaxLength(150);
        b.Property(r => r.GuardianRelationship).HasMaxLength(50);
        b.Property(r => r.GuardianPhone).HasMaxLength(20);
        b.Property(r => r.GuardianAddress).HasMaxLength(300);
        b.HasIndex(r => r.Lrn).IsUnique();
        b.HasIndex(r => new { r.GradeLevel, r.LastName });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_admission_records_grade_level", "grade_level IN (11, 12)");
            t.HasCheckConstraint("ck_admission_records_lrn", "CHAR_LENGTH(lrn) = 12");
        });
    }
}

internal sealed class AdmissionDocumentConfiguration : IEntityTypeConfiguration<AdmissionDocument>
{
    public void Configure(EntityTypeBuilder<AdmissionDocument> b)
    {
        b.ToTable("admission_documents");
        b.Property(d => d.Kind).HasMaxLength(60).IsRequired();
        b.Property(d => d.FileName).HasMaxLength(255);
        b.Property(d => d.ContentType).HasMaxLength(100);
        b.Property(d => d.Data).HasColumnType("longblob");
        b.HasOne(d => d.Record).WithMany(r => r.Documents).HasForeignKey(d => d.RecordId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(d => new { d.RecordId, d.Kind });
    }
}
