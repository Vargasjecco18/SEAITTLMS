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
        b.Property(r => r.Sex).HasConversion<string>().HasMaxLength(20);
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
        b.Property(r => r.Email).HasMaxLength(150);
        b.Property(r => r.AddressStreet).HasMaxLength(150);
        b.Property(r => r.AddressCity).HasMaxLength(100);
        b.Property(r => r.AddressProvince).HasMaxLength(100);
        b.Property(r => r.AddressPostalCode).HasMaxLength(10);
        b.Property(r => r.FatherOccupation).HasMaxLength(100);
        b.Property(r => r.FatherEmail).HasMaxLength(150);
        b.Property(r => r.MotherOccupation).HasMaxLength(100);
        b.Property(r => r.MotherEmail).HasMaxLength(150);
        b.Property(r => r.ParentsAddress).HasMaxLength(300);
        b.Property(r => r.ParentsStreet).HasMaxLength(150);
        b.Property(r => r.ParentsCity).HasMaxLength(100);
        b.Property(r => r.ParentsProvince).HasMaxLength(100);
        b.Property(r => r.ParentsPostalCode).HasMaxLength(10);
        b.Property(r => r.GuardianOccupation).HasMaxLength(100);
        b.Property(r => r.GuardianEmail).HasMaxLength(150);
        b.Property(r => r.GuardianStreet).HasMaxLength(150);
        b.Property(r => r.GuardianCity).HasMaxLength(100);
        b.Property(r => r.GuardianProvince).HasMaxLength(100);
        b.Property(r => r.GuardianPostalCode).HasMaxLength(10);
        b.HasOne(r => r.Track).WithMany().HasForeignKey(r => r.TrackId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(r => r.Strand).WithMany().HasForeignKey(r => r.StrandId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(r => r.Lrn).IsUnique();
        b.HasIndex(r => new { r.GradeLevel, r.LastName });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_admission_records_grade_level", "grade_level IN (11, 12)");
            t.HasCheckConstraint("ck_admission_records_lrn", "CHAR_LENGTH(lrn) = 12");
        });
    }
}

internal sealed class AdmissionOptionConfiguration : IEntityTypeConfiguration<AdmissionOption>
{
    public void Configure(EntityTypeBuilder<AdmissionOption> b)
    {
        b.ToTable("admission_options");
        b.Property(o => o.Kind).HasMaxLength(20).IsRequired();
        b.Property(o => o.Name).HasMaxLength(AdmissionOptionKinds.MaxNameLength).IsRequired();
        b.HasIndex(o => new { o.Kind, o.Name }).IsUnique();
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
