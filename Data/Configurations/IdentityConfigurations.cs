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

        b.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        b.Property(u => u.MiddleName).HasMaxLength(100);
        b.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        b.Property(u => u.NameExtension).HasMaxLength(10);

        b.Property(u => u.SecurityStamp).HasMaxLength(64).IsRequired();
        b.Ignore(u => u.FullName);

        b.HasOne(u => u.Role)
            .WithMany()
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        b.ToTable(t => t.HasCheckConstraint("ck_users_failed_login_count", "failed_login_count >= 0"));
    }
}

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> b)
    {
        b.HasOne(s => s.User)
            .WithOne()
            .HasForeignKey<Student>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(s => s.UserId).IsUnique();

        b.Property(s => s.Lrn).HasMaxLength(12).IsFixedLength().IsRequired();
        b.HasIndex(s => s.Lrn).IsUnique();

        b.Property(s => s.Sex).HasConversion<string>().HasMaxLength(10);

        b.ToTable(t => t.HasCheckConstraint("ck_students_lrn", "lrn REGEXP '^[0-9]{12}$'"));
    }
}

/// <summary>Teachers, registrars, guidance counselors and administrators all look the same for now.</summary>
internal sealed class StaffProfileConfiguration<T> : IEntityTypeConfiguration<T> where T : StaffProfile
{
    public void Configure(EntityTypeBuilder<T> b)
    {
        b.HasOne(p => p.User)
            .WithOne()
            .HasForeignKey<T>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(p => p.UserId).IsUnique();

        b.Property(p => p.EmployeeNumber).HasMaxLength(30);
        b.HasIndex(p => p.EmployeeNumber).IsUnique();
    }
}
