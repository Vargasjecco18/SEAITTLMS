using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data;

internal sealed class ReferralConfiguration : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> b)
    {
        b.Property(r => r.Reason).HasMaxLength(20).IsRequired();
        b.Property(r => r.Detail).HasMaxLength(300).IsRequired();
        b.Property(r => r.Metric).HasPrecision(6, 2);
        b.Property(r => r.Status).HasMaxLength(12).IsRequired();
        b.Property(r => r.Intervention).HasMaxLength(100);
        b.Property(r => r.Notes).HasMaxLength(1000);
        b.Property(r => r.Outcome).HasMaxLength(12);

        // A referral goes with its student. If the subject is removed, the referral stays without it.
        b.HasOne(r => r.Student).WithMany().HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.Assignment).WithMany().HasForeignKey(r => r.AssignmentId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(r => r.HandledBy).WithMany().HasForeignKey(r => r.HandledByUserId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(r => new { r.StudentId, r.Status });
        b.HasIndex(r => new { r.Status, r.Reason });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_referrals_reason", "reason IN ('FailingGrades', 'AtRisk', 'LowAttendance')");
            t.HasCheckConstraint("ck_referrals_status", "status IN ('Open', 'InProgress', 'Resolved')");
            t.HasCheckConstraint("ck_referrals_outcome", "outcome IS NULL OR outcome IN ('Improved', 'NoChange')");
        });
    }
}

internal sealed class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<WithdrawalRequest> b)
    {
        b.Property(w => w.Reason).HasMaxLength(500).IsRequired();
        b.Property(w => w.Status).HasMaxLength(10).IsRequired();
        b.Property(w => w.DecisionNote).HasMaxLength(500);

        b.HasOne(w => w.Student).WithMany().HasForeignKey(w => w.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(w => w.DecidedBy).WithMany().HasForeignKey(w => w.DecidedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(w => new { w.Status, w.StudentId });

        b.ToTable(t => t.HasCheckConstraint("ck_withdrawal_requests_status", "status IN ('Pending', 'Approved', 'Declined', 'Cancelled')"));
    }
}
