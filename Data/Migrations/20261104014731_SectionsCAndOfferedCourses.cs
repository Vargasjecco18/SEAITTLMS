using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class SectionsCAndOfferedCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_offered",
                table: "strand_subjects",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            // A section C for every strand and grade level of the school years that are not over (A and B are already there; the registrar chooses between A, B and C).
            migrationBuilder.Sql(
                "INSERT INTO `sections` (`school_year_id`, `strand_id`, `grade_level`, `name`, `adviser_teacher_id`, `created_at_utc`, `updated_at_utc`) " +
                "SELECT s.`school_year_id`, s.`strand_id`, s.`grade_level`, 'C', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) " +
                "FROM `sections` s JOIN `school_years` y ON y.`id` = s.`school_year_id` " +
                "WHERE s.`name` = 'A' AND y.`end_date` >= CURDATE() " +
                "AND NOT EXISTS (SELECT 1 FROM `sections` c WHERE c.`school_year_id` = s.`school_year_id` AND c.`strand_id` = s.`strand_id` AND c.`grade_level` = s.`grade_level` AND c.`name` = 'C');");

            // The practice announcement "Summative exams start next Monday" (the summative exams of the provider have their own announcement now) is removed.
            migrationBuilder.Sql(
                "DELETE FROM `announcements` WHERE `id` > 0 AND `title` = 'Summative exams start next Monday' " +
                "AND `author_user_id` IN (SELECT `id` FROM `users` WHERE `role_id` = 6);");
            migrationBuilder.Sql(
                "DELETE FROM `notifications` WHERE `id` > 0 AND `kind` = 'announcement' AND `title` = 'Summative exams start next Monday';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_offered",
                table: "strand_subjects");
        }
    }
}
