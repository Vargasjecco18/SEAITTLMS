using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <summary>
    /// Puts sections A to E in the database, so the registrar picks a section (A, B, C, D or E) instead of typing it and a
    /// teacher can be given a section at once. For the current school year (the one of the current term, else the latest), for
    /// every schedule of every strand, at the grade levels the strand has courses for. A section that is already there is left
    /// alone, so running it again, or on a database that has some of them, adds only what is missing.
    /// </summary>
    public partial class AddSectionsAToE : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "INSERT INTO `sections` (`school_year_id`, `strand_id`, `grade_level`, `schedule_id`, `name`, `created_at_utc`, `updated_at_utc`) " +
                "SELECT y.`id`, ss.`strand_id`, c.`grade_level`, ss.`schedule_id`, n.`name`, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) " +
                "FROM (SELECT COALESCE((SELECT `school_year_id` FROM `terms` WHERE `is_current` = 1 ORDER BY `id` LIMIT 1), " +
                "                      (SELECT `id` FROM `school_years` ORDER BY `start_date` DESC, `id` DESC LIMIT 1)) AS `id`) y " +
                "JOIN `strand_schedules` ss " +
                "JOIN (SELECT DISTINCT `strand_id`, `grade_level` FROM `strand_subjects`) c ON c.`strand_id` = ss.`strand_id` " +
                "JOIN (SELECT 'A' AS `name` UNION ALL SELECT 'B' UNION ALL SELECT 'C' UNION ALL SELECT 'D' UNION ALL SELECT 'E') n " +
                "WHERE y.`id` IS NOT NULL " +
                "  AND NOT EXISTS (SELECT 1 FROM `sections` x WHERE x.`school_year_id` = y.`id` AND x.`strand_id` = ss.`strand_id` " +
                "                  AND x.`grade_level` = c.`grade_level` AND x.`schedule_id` = ss.`schedule_id` AND x.`name` = n.`name`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only the sections nobody uses: a section with students or a teacher stays.
            migrationBuilder.Sql(
                "DELETE FROM `sections` WHERE `name` IN ('A', 'B', 'C', 'D', 'E') " +
                "AND NOT EXISTS (SELECT 1 FROM `enrollments` e WHERE e.`section_id` = `sections`.`id`) " +
                "AND NOT EXISTS (SELECT 1 FROM `teaching_assignments` a WHERE a.`section_id` = `sections`.`id`);");
        }
    }
}
