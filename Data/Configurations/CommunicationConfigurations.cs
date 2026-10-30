using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.Property(m => m.Body).HasMaxLength(2000).IsRequired();

        // A deleted account takes its messages with it.
        b.HasOne(m => m.Sender).WithMany().HasForeignKey(m => m.SenderUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(m => m.Recipient).WithMany().HasForeignKey(m => m.RecipientUserId).OnDelete(DeleteBehavior.Cascade);

        // The unread count and a conversation are the two things that are asked all the time.
        b.HasIndex(m => new { m.RecipientUserId, m.ReadAtUtc });
        b.HasIndex(m => new { m.SenderUserId, m.RecipientUserId, m.CreatedAtUtc });
    }
}

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.Property(n => n.Kind).HasMaxLength(30).IsRequired();
        b.Property(n => n.Title).HasMaxLength(200).IsRequired();
        b.Property(n => n.Body).HasMaxLength(500);
        b.Property(n => n.Link).HasMaxLength(300);

        b.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAtUtc });
    }
}

internal sealed class NotificationSoundConfiguration : IEntityTypeConfiguration<NotificationSound>
{
    public void Configure(EntityTypeBuilder<NotificationSound> b)
    {
        b.Property(n => n.Name).HasMaxLength(255).IsRequired();
        b.Property(n => n.ContentType).HasMaxLength(100).IsRequired();
        b.Property(n => n.Data).HasColumnType("mediumblob").IsRequired();

        // One sound for each person, and one for the school (no person): a deleted account takes its sound with it.
        b.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(n => n.UserId).IsUnique();
    }
}

internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> b)
    {
        b.Property(a => a.Title).HasMaxLength(200).IsRequired();
        b.Property(a => a.Body).HasColumnType("text").IsRequired();
        b.Property(a => a.Audience).HasMaxLength(10).IsRequired();
        b.Property(a => a.Category).HasMaxLength(20).IsRequired().HasDefaultValue(AnnouncementCategories.Other);

        // A deleted account takes its announcements with it, and so does a deleted section.
        b.HasOne(a => a.Author).WithMany().HasForeignKey(a => a.AuthorUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(a => a.Section).WithMany().HasForeignKey(a => a.SectionId).OnDelete(DeleteBehavior.Cascade);

        // A teacher's announcement for a course goes with the course.
        b.HasOne(a => a.Assignment).WithMany().HasForeignKey(a => a.AssignmentId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(a => new { a.Audience, a.CreatedAtUtc });
        b.HasIndex(a => new { a.AuthorUserId, a.CreatedAtUtc });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_announcements_audience", "audience IN ('Everyone', 'Students', 'Teachers')");
            t.HasCheckConstraint("ck_announcements_category", "category IN ('Exam', 'Holiday', 'SemesterBreak', 'TeachersMeeting', 'Other')");
        });
    }
}
