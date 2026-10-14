using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClassTimesForSeededSectionsAndSharedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendar_entry_invites",
                columns: table => new
                {
                    entry_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_entry_invites", x => new { x.entry_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_calendar_entry_invites_calendar_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "calendar_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_calendar_entry_invites_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_entry_invites_user_id",
                table: "calendar_entry_invites",
                column: "user_id");

            // ---- The class time of every course, for the sections that have none yet. A section A meets in the morning (AM), B at midday (MID) and C in the afternoon (PM);
            // a course meets on MWF or TTH for an hour, inside the hours of its part of the day. Two courses of a section never meet at the same time in the same
            // term (the courses of a term take turns: MWF and TTH alternate, and an hour of the day is used by at most one course of each of the two day groups), and the
            // start moves from one section to the next, so the same course in two sections does not meet at the same time. A section that already has a class time (or
            // a teacher: the class schedule of those is the coordinator's) is left alone. The academic coordinator changes any of it in the Class schedule tab.
            migrationBuilder.Sql(@"INSERT INTO `course_schedules` (`section_id`, `subject_id`, `schedule_id`, `days`, `start_time`, `end_time`, `created_at_utc`, `updated_at_utc`)
SELECT r.`section_id`, r.`subject_id`, r.`schedule_id`,
       IF(MOD(r.`place` + r.`k`, 2) = 1, 'MWF', 'TTH'),
       ADDTIME(r.`window_start`, SEC_TO_TIME(MOD(FLOOR((r.`place` - 1) / 2) + r.`k`, r.`hours`) * 3600)),
       ADDTIME(r.`window_start`, SEC_TO_TIME((MOD(FLOOR((r.`place` - 1) / 2) + r.`k`, r.`hours`) + 1) * 3600)),
       UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM (
    SELECT c.`section_id`, c.`subject_id`, c.`place`, c.`k`, sc.`id` AS `schedule_id`, sc.`start_time` AS `window_start`,
           GREATEST(1, FLOOR(TIME_TO_SEC(TIMEDIFF(sc.`end_time`, sc.`start_time`)) / 3600)) AS `hours`
    FROM (
        SELECT s.`id` AS `section_id`, ss.`subject_id`,
               ROW_NUMBER() OVER (PARTITION BY s.`id`, ss.`term_number` ORDER BY ss.`subject_id`) AS `place`,
               DENSE_RANK() OVER (ORDER BY s.`id`) AS `k`,
               CASE LEFT(s.`name`, 1) WHEN 'B' THEN 'MID' WHEN 'C' THEN 'PM' ELSE 'AM' END AS `part`
        FROM `sections` s
        JOIN (SELECT `strand_id`, `grade_level`, `subject_id`, MIN(`term_number`) AS `term_number` FROM `strand_subjects` GROUP BY `strand_id`, `grade_level`, `subject_id`) ss ON ss.`strand_id` = s.`strand_id` AND ss.`grade_level` = s.`grade_level`
        WHERE NOT EXISTS (SELECT 1 FROM `course_schedules` x WHERE x.`section_id` = s.`id`)
          AND NOT EXISTS (SELECT 1 FROM `teaching_assignments` t WHERE t.`section_id` = s.`id`)
    ) c
    JOIN `schedules` sc ON sc.`code` = c.`part`
) r
WHERE r.`place` <= 2 * r.`hours`
  AND r.`window_start` IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_entry_invites");
        }
    }
}
