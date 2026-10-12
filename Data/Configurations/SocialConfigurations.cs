using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data;

internal sealed class ProfilePhotoConfiguration : IEntityTypeConfiguration<ProfilePhoto>
{
    public void Configure(EntityTypeBuilder<ProfilePhoto> b)
    {
        b.ToTable("profile_photos");
        b.HasKey(p => p.UserId);
        b.Property(p => p.ContentType).HasMaxLength(50).IsRequired();

        // At most 16 MB by the column type; the service keeps it to 1 MB.
        b.Property(p => p.Data).HasColumnType("mediumblob").IsRequired();

        b.HasOne(p => p.User).WithOne().HasForeignKey<ProfilePhoto>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> b)
    {
        b.ToTable("friendships");
        b.Property(f => f.Status).HasMaxLength(10).IsRequired();

        b.HasOne(f => f.Requester).WithMany().HasForeignKey(f => f.RequesterUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(f => f.Addressee).WithMany().HasForeignKey(f => f.AddresseeUserId).OnDelete(DeleteBehavior.Cascade);

        // One row for two people (the service never writes the other direction). The second index answers "my requests".
        b.HasIndex(f => new { f.RequesterUserId, f.AddresseeUserId }).IsUnique();
        b.HasIndex(f => new { f.AddresseeUserId, f.Status });

        // (A check that the two people differ can't be written: MySQL does not allow a check on a column that has a cascading
        // foreign key. FriendService refuses to befriend oneself.)
        b.ToTable(t => t.HasCheckConstraint("ck_friendships_status", "status IN ('Pending', 'Accepted')"));
    }
}

internal sealed class StudyGroupConfiguration : IEntityTypeConfiguration<StudyGroup>
{
    public void Configure(EntityTypeBuilder<StudyGroup> b)
    {
        b.ToTable("study_groups");
        b.Property(g => g.Name).HasMaxLength(100).IsRequired();
        b.Property(g => g.Description).HasMaxLength(500);

        // A deleted account takes the groups it owns with it.
        b.HasOne(g => g.Owner).WithMany().HasForeignKey(g => g.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(g => g.OwnerUserId);
    }
}

internal sealed class StudyGroupMemberConfiguration : IEntityTypeConfiguration<StudyGroupMember>
{
    public void Configure(EntityTypeBuilder<StudyGroupMember> b)
    {
        b.ToTable("study_group_members");
        b.HasKey(m => new { m.GroupId, m.UserId });

        b.HasOne(m => m.Group).WithMany(g => g.Members).HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(m => m.UserId);
    }
}

internal sealed class GroupPostConfiguration : IEntityTypeConfiguration<GroupPost>
{
    public void Configure(EntityTypeBuilder<GroupPost> b)
    {
        b.ToTable("group_posts");
        b.Property(p => p.Body).HasMaxLength(2000).IsRequired();

        b.HasOne(p => p.Group).WithMany(g => g.Posts).HasForeignKey(p => p.GroupId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(p => p.Author).WithMany().HasForeignKey(p => p.AuthorUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(p => p.Parent).WithMany().HasForeignKey(p => p.ParentPostId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(p => new { p.GroupId, p.CreatedAtUtc });
    }
}

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> b)
    {
        b.ToTable("stored_files");
        b.Property(f => f.Name).HasMaxLength(255).IsRequired();
        b.Property(f => f.ContentType).HasMaxLength(150).IsRequired();

        b.HasOne(f => f.Owner).WithMany().HasForeignKey(f => f.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(f => f.Content).WithOne(c => c.File).HasForeignKey<StoredFileContent>(c => c.FileId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(f => new { f.OwnerUserId, f.CreatedAtUtc });

        b.ToTable(t => t.HasCheckConstraint("ck_stored_files_size", "size_bytes >= 0"));
    }
}

internal sealed class StoredFileContentConfiguration : IEntityTypeConfiguration<StoredFileContent>
{
    public void Configure(EntityTypeBuilder<StoredFileContent> b)
    {
        b.ToTable("stored_file_contents");
        b.HasKey(c => c.FileId);

        // At most 4 GB by the column type; the service keeps a file to the size the administrator sets (10 MB at first).
        b.Property(c => c.Data).HasColumnType("longblob").IsRequired();
    }
}

internal sealed class SharedFileConfiguration : IEntityTypeConfiguration<SharedFile>
{
    public void Configure(EntityTypeBuilder<SharedFile> b)
    {
        b.ToTable("shared_files");

        b.HasOne(s => s.File).WithMany(f => f.Shares).HasForeignKey(s => s.FileId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.SharedWithUser).WithMany().HasForeignKey(s => s.SharedWithUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.Group).WithMany().HasForeignKey(s => s.GroupId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.Assignment).WithMany().HasForeignKey(s => s.AssignmentId).OnDelete(DeleteBehavior.Cascade);

        // A file is shared once with the same person, group or course (an empty value is never equal to another one, so each index only
        // looks at its own kind of share).
        b.HasIndex(s => new { s.FileId, s.SharedWithUserId }).IsUnique();
        b.HasIndex(s => new { s.FileId, s.GroupId }).IsUnique();
        b.HasIndex(s => new { s.FileId, s.AssignmentId }).IsUnique();
        b.HasIndex(s => s.SharedWithUserId);
        b.HasIndex(s => s.GroupId);
        b.HasIndex(s => s.AssignmentId);

        // (That exactly one of the three is set is kept by FileService: MySQL does not allow a check on columns that have a cascading
        // foreign key.)
    }
}

internal sealed class SecurityLogConfiguration : IEntityTypeConfiguration<SecurityLog>
{
    public void Configure(EntityTypeBuilder<SecurityLog> b)
    {
        b.ToTable("security_logs");
        b.Property(l => l.Kind).HasMaxLength(40).IsRequired();
        b.Property(l => l.Severity).HasMaxLength(10).IsRequired();
        b.Property(l => l.Email).HasMaxLength(254);
        b.Property(l => l.ActorName).HasMaxLength(200);
        b.Property(l => l.IpAddress).HasMaxLength(45);
        b.Property(l => l.UserAgent).HasMaxLength(300);
        b.Property(l => l.Details).HasMaxLength(1000);

        // The log outlives the accounts: when one is deleted the line stays (with the email), only the link is emptied.
        b.HasOne<User>().WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(l => l.ActorUserId).OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(l => l.CreatedAtUtc);
        b.HasIndex(l => new { l.Kind, l.CreatedAtUtc });
        b.HasIndex(l => l.UserId);
    }
}

internal sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.ToTable("system_settings");
        b.HasKey(s => s.Key);
        b.Property(s => s.Key).HasMaxLength(80);
        b.Property(s => s.Value).HasMaxLength(2000).IsRequired();
        b.HasOne<User>().WithMany().HasForeignKey(s => s.UpdatedByUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
