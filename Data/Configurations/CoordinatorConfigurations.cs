using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Data;

internal sealed class MaterialModuleConfiguration : IEntityTypeConfiguration<MaterialModule>
{
    public void Configure(EntityTypeBuilder<MaterialModule> b)
    {
        b.ToTable("material_modules");
        b.Property(m => m.Title).HasMaxLength(200).IsRequired();
        b.Property(m => m.Description).HasMaxLength(1000);
        b.Property(m => m.Body).HasColumnType("text");
        b.Property(m => m.LinkUrl).HasMaxLength(500);

        b.HasOne(m => m.Subject).WithMany().HasForeignKey(m => m.SubjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(m => m.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(m => new { m.SubjectId, m.Position });
    }
}

internal sealed class MaterialItemConfiguration : IEntityTypeConfiguration<MaterialItem>
{
    public void Configure(EntityTypeBuilder<MaterialItem> b)
    {
        b.ToTable("material_items");
        b.Property(i => i.Kind).HasMaxLength(20).IsRequired();
        b.Property(i => i.Title).HasMaxLength(200).IsRequired();
        b.Property(i => i.Instructions).HasColumnType("text");
        b.Property(i => i.MaxScore).HasPrecision(6, 2);

        b.HasOne(i => i.Module).WithMany(m => m.Items).HasForeignKey(i => i.ModuleId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(i => new { i.ModuleId, i.Position });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_material_items_kind", "kind IN ('Activity', 'Quiz', 'TaskPerformance')");
            t.HasCheckConstraint("ck_material_items_max_score", "max_score > 0");
        });
    }
}

internal sealed class MaterialQuestionConfiguration : IEntityTypeConfiguration<MaterialQuestion>
{
    public void Configure(EntityTypeBuilder<MaterialQuestion> b)
    {
        b.ToTable("material_questions");
        b.Property(q => q.Text).HasMaxLength(1000).IsRequired();
        b.Property(q => q.Kind).HasMaxLength(20).IsRequired();
        b.Property(q => q.OptionsText).HasColumnType("text");
        b.Property(q => q.CorrectAnswer).HasMaxLength(500).IsRequired();
        b.Property(q => q.Points).HasPrecision(6, 2);

        b.HasOne(q => q.Item).WithMany(i => i.Questions).HasForeignKey(q => q.ItemId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(q => new { q.ItemId, q.Position });

        b.ToTable(t => t.HasCheckConstraint("ck_material_questions_kind", "kind IN ('MultipleChoice', 'TrueFalse', 'ShortAnswer')"));
    }
}

internal sealed class MaterialDeliveryConfiguration : IEntityTypeConfiguration<MaterialDelivery>
{
    public void Configure(EntityTypeBuilder<MaterialDelivery> b)
    {
        b.ToTable("material_deliveries");

        b.HasOne(d => d.MaterialModule).WithMany(m => m.Deliveries).HasForeignKey(d => d.MaterialModuleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(d => d.Assignment).WithMany().HasForeignKey(d => d.AssignmentId).OnDelete(DeleteBehavior.Cascade);

        // The teacher's copy goes when the teacher deletes the module (or their course).
        b.HasOne(d => d.Module).WithMany().HasForeignKey(d => d.ModuleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(d => d.SentByUserId).OnDelete(DeleteBehavior.SetNull);

        // A library module goes to a teacher's course once.
        b.HasIndex(d => new { d.MaterialModuleId, d.AssignmentId }).IsUnique();
        b.HasIndex(d => d.ModuleId).IsUnique();
    }
}

internal sealed class TeacherEvaluationConfiguration : IEntityTypeConfiguration<TeacherEvaluation>
{
    public void Configure(EntityTypeBuilder<TeacherEvaluation> b)
    {
        b.ToTable("teacher_evaluations");
        b.Property(e => e.Title).HasMaxLength(200).IsRequired();
        b.Property(e => e.Instructions).HasMaxLength(2000);

        b.HasOne(e => e.Term).WithMany().HasForeignKey(e => e.TermId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(e => e.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(e => e.SentAtUtc);
    }
}

internal sealed class EvaluationQuestionConfiguration : IEntityTypeConfiguration<EvaluationQuestion>
{
    public void Configure(EntityTypeBuilder<EvaluationQuestion> b)
    {
        b.ToTable("evaluation_questions");
        b.Property(q => q.Text).HasMaxLength(500).IsRequired();
        b.Property(q => q.Kind).HasMaxLength(12).IsRequired();
        b.Property(q => q.OptionsText).HasColumnType("text");
        b.Property(q => q.Category).HasMaxLength(60);

        b.HasOne(q => q.Evaluation).WithMany(e => e.Questions).HasForeignKey(q => q.EvaluationId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(q => new { q.EvaluationId, q.Position });

        b.ToTable(t => t.HasCheckConstraint("ck_evaluation_questions_kind", "kind IN ('Likert', 'Performance', 'Frequency', 'Binary', 'Ranking', 'SelectAll', 'Comment')"));
    }
}

internal sealed class EvaluationTargetConfiguration : IEntityTypeConfiguration<EvaluationTarget>
{
    public void Configure(EntityTypeBuilder<EvaluationTarget> b)
    {
        b.ToTable("evaluation_targets");
        b.HasKey(t => new { t.EvaluationId, t.AssignmentId });
        b.HasOne(t => t.Evaluation).WithMany(e => e.Targets).HasForeignKey(t => t.EvaluationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(t => t.Assignment).WithMany().HasForeignKey(t => t.AssignmentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EvaluationResponseConfiguration : IEntityTypeConfiguration<EvaluationResponse>
{
    public void Configure(EntityTypeBuilder<EvaluationResponse> b)
    {
        b.ToTable("evaluation_responses");

        b.HasOne(r => r.Evaluation).WithMany(e => e.Responses).HasForeignKey(r => r.EvaluationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.Assignment).WithMany().HasForeignKey(r => r.AssignmentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.Student).WithMany().HasForeignKey(r => r.StudentId).OnDelete(DeleteBehavior.Cascade);

        // A student answers once for each teacher's course.
        b.HasIndex(r => new { r.EvaluationId, r.AssignmentId, r.StudentId }).IsUnique();
        b.HasIndex(r => new { r.StudentId, r.EvaluationId });
    }
}

internal sealed class EvaluationAnswerConfiguration : IEntityTypeConfiguration<EvaluationAnswer>
{
    public void Configure(EntityTypeBuilder<EvaluationAnswer> b)
    {
        b.ToTable("evaluation_answers");
        b.HasKey(a => new { a.ResponseId, a.QuestionId });
        b.Property(a => a.Comment).HasMaxLength(1000);
        b.Property(a => a.Selection).HasMaxLength(100);

        b.HasOne(a => a.Response).WithMany(r => r.Answers).HasForeignKey(a => a.ResponseId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(a => a.Question).WithMany().HasForeignKey(a => a.QuestionId).OnDelete(DeleteBehavior.Cascade);

        b.ToTable(t => t.HasCheckConstraint("ck_evaluation_answers_rating", "rating IS NULL OR rating BETWEEN 1 AND 5"));
    }
}
