using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data;

internal sealed class TeachingAssignmentConfiguration : IEntityTypeConfiguration<TeachingAssignment>
{
    public void Configure(EntityTypeBuilder<TeachingAssignment> b)
    {
        // Removing a teacher removes their subjects (and what is under them).
        b.HasOne(a => a.Teacher).WithMany().HasForeignKey(a => a.TeacherId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(a => a.Section).WithMany().HasForeignKey(a => a.SectionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.Subject).WithMany().HasForeignKey(a => a.SubjectId).OnDelete(DeleteBehavior.Restrict);

        // A subject of a section has one teacher.
        b.HasIndex(a => new { a.SectionId, a.SubjectId }).IsUnique();
    }
}

internal sealed class LearningModuleConfiguration : IEntityTypeConfiguration<LearningModule>
{
    public void Configure(EntityTypeBuilder<LearningModule> b)
    {
        b.ToTable("modules");
        b.Property(m => m.Title).HasMaxLength(200).IsRequired();
        b.Property(m => m.Description).HasMaxLength(1000);
        b.Property(m => m.Body).HasColumnType("text");
        b.Property(m => m.LinkUrl).HasMaxLength(500);
        b.Property(m => m.IsSyllabus).HasDefaultValue(false);

        b.HasOne(m => m.Assignment).WithMany(a => a.Modules).HasForeignKey(m => m.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(m => new { m.AssignmentId, m.Position });
        b.HasIndex(m => m.VisibleFromUtc);
    }
}

internal sealed class ModuleItemConfiguration : IEntityTypeConfiguration<ModuleItem>
{
    public void Configure(EntityTypeBuilder<ModuleItem> b)
    {
        b.Property(i => i.Kind).HasMaxLength(20).IsRequired();
        b.Property(i => i.Title).HasMaxLength(200).IsRequired();
        b.Property(i => i.Instructions).HasColumnType("text");
        b.Property(i => i.MaxScore).HasPrecision(6, 2);

        b.HasOne(i => i.Module).WithMany(m => m.Items).HasForeignKey(i => i.ModuleId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(i => new { i.ModuleId, i.Kind, i.Position });
        b.HasIndex(i => i.DueAtUtc);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_module_items_kind", "kind IN ('Activity', 'Quiz', 'TaskPerformance')");
            t.HasCheckConstraint("ck_module_items_max_score", "max_score > 0");
        });
    }
}

internal sealed class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> b)
    {
        b.Property(q => q.Text).HasMaxLength(1000).IsRequired();
        b.Property(q => q.Kind).HasMaxLength(20).IsRequired();
        b.Property(q => q.OptionsText).HasColumnType("text");
        b.Property(q => q.CorrectAnswer).HasMaxLength(500).IsRequired();
        b.Property(q => q.Points).HasPrecision(6, 2);

        b.HasOne(q => q.Item).WithMany(i => i.Questions).HasForeignKey(q => q.ItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(q => new { q.ItemId, q.Position });

        b.ToTable(t => t.HasCheckConstraint("ck_quiz_questions_kind", "kind IN ('MultipleChoice', 'TrueFalse', 'ShortAnswer')"));
    }
}

internal sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> b)
    {
        b.Property(s => s.Status).HasMaxLength(20).IsRequired();
        b.Property(s => s.AnswerText).HasColumnType("text");
        b.Property(s => s.LinkUrl).HasMaxLength(500);
        b.Property(s => s.AnswersJson).HasColumnType("text");
        b.Property(s => s.Score).HasPrecision(6, 2);
        b.Property(s => s.Feedback).HasMaxLength(1000);
        b.Property(s => s.ViolationLog).HasColumnType("text");
        b.Property(s => s.AutoSubmitReason).HasMaxLength(20);

        // The work goes with its item and with its student.
        b.HasOne(s => s.Item).WithMany(i => i.Submissions).HasForeignKey(s => s.ItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.Student).WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Cascade);

        // One piece of work per student and item.
        b.HasIndex(s => new { s.ItemId, s.StudentId }).IsUnique();
        b.HasIndex(s => new { s.StudentId, s.Status });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_submissions_status", "status IN ('InProgress', 'Submitted', 'Graded')");
            t.HasCheckConstraint("ck_submissions_score", "score IS NULL OR score >= 0");
        });
    }
}

internal sealed class ModuleViewConfiguration : IEntityTypeConfiguration<ModuleView>
{
    public void Configure(EntityTypeBuilder<ModuleView> b)
    {
        b.ToTable("module_views");
        b.HasKey(v => new { v.ModuleId, v.StudentId });
        b.HasOne(v => v.Module).WithMany().HasForeignKey(v => v.ModuleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(v => v.Student).WithMany().HasForeignKey(v => v.StudentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ItemViewConfiguration : IEntityTypeConfiguration<ItemView>
{
    public void Configure(EntityTypeBuilder<ItemView> b)
    {
        b.ToTable("item_views");
        b.HasKey(v => new { v.ItemId, v.StudentId });
        b.HasOne(v => v.Item).WithMany().HasForeignKey(v => v.ItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(v => v.Student).WithMany().HasForeignKey(v => v.StudentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(v => v.StudentId);
    }
}

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> b)
    {
        b.Property(a => a.Status).HasMaxLength(10).IsRequired();
        b.Property(a => a.Note).HasMaxLength(200);

        // Attendance goes with its subject and with its student.
        b.HasOne(a => a.Assignment).WithMany().HasForeignKey(a => a.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(a => a.Student).WithMany().HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Cascade);

        // One mark per student, subject and day.
        b.HasIndex(a => new { a.AssignmentId, a.StudentId, a.AttendedOn }).IsUnique();
        b.HasIndex(a => new { a.StudentId, a.AttendedOn });

        b.ToTable(t => t.HasCheckConstraint("ck_attendance_records_status", "status IN ('Present', 'Late', 'Absent')"));
    }
}
