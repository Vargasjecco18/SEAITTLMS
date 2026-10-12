using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <summary>
    /// The questions of a teacher evaluation can be answered in more ways: the 5-point Likert, performance and frequency scales,
    /// a Yes/No (binary) answer, a ranking of options and a select-all-that-apply list. The old "Rating 1 to 5" questions become performance-scale questions.
    /// </summary>
    public partial class AddEvaluationScales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_evaluation_questions_kind",
                table: "evaluation_questions");

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                table: "evaluation_questions",
                type: "varchar(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "options_text",
                table: "evaluation_questions",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "selection",
                table: "evaluation_answers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // The "Rating 1 to 5" questions of the earlier version (Poor to Excellent) are the performance scale now.
            migrationBuilder.Sql("UPDATE `evaluation_questions` SET `kind` = 'Performance' WHERE `id` > 0 AND `kind` = 'Rating';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evaluation_questions_kind",
                table: "evaluation_questions",
                sql: "kind IN ('Likert', 'Performance', 'Frequency', 'Binary', 'Ranking', 'SelectAll', 'Comment')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_evaluation_questions_kind",
                table: "evaluation_questions");

            // The kinds of question the earlier version did not have go back to what they are closest to; the ones with no 1 to 5 answer are removed (with their answers).
            migrationBuilder.Sql("DELETE FROM `evaluation_questions` WHERE `id` > 0 AND `kind` IN ('Binary', 'Ranking', 'SelectAll');");
            migrationBuilder.Sql("UPDATE `evaluation_questions` SET `kind` = 'Rating' WHERE `id` > 0 AND `kind` IN ('Likert', 'Performance', 'Frequency');");

            migrationBuilder.DropColumn(
                name: "options_text",
                table: "evaluation_questions");

            migrationBuilder.DropColumn(
                name: "selection",
                table: "evaluation_answers");

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                table: "evaluation_questions",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(12)",
                oldMaxLength: 12)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_evaluation_questions_kind",
                table: "evaluation_questions",
                sql: "kind IN ('Rating', 'Comment')");
        }
    }
}
