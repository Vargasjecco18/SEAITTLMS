using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnitsNameExtensionAndClassTimeSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Units of a course. Every course that exists gets 3 units (the academic coordinator edits them in the Courses tab).
            migrationBuilder.AddColumn<decimal>(
                name: "units",
                table: "subjects",
                type: "decimal(3,1)",
                nullable: false,
                defaultValue: 3m);

            migrationBuilder.AlterColumn<decimal>(
                name: "units",
                table: "subjects",
                type: "decimal(3,1)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(3,1)",
                oldDefaultValue: 3m);

            migrationBuilder.AddCheckConstraint(
                name: "ck_subjects_units",
                table: "subjects",
                sql: "units >= 0.5 AND units <= 12");

            // ---- The name extension (Jr., Sr., III) written on the enrollment form.
            migrationBuilder.AddColumn<string>(
                name: "name_extension",
                table: "enrollments",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // ---- The day part (AM, MID, PM) moves from the section to each course's class time.
            // A class time that exists keeps the schedule of its section; when the section had none, the first schedule whose hours hold the class; else the first schedule.
            migrationBuilder.AddColumn<int>(
                name: "schedule_id",
                table: "course_schedules",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"UPDATE `course_schedules` c JOIN `sections` s ON s.`id` = c.`section_id` SET c.`schedule_id` = s.`schedule_id` WHERE s.`schedule_id` IS NOT NULL AND c.`id` > 0;");
            migrationBuilder.Sql(@"UPDATE `course_schedules` c SET c.`schedule_id` = (SELECT sc.`id` FROM `schedules` sc WHERE c.`start_time` >= sc.`start_time` AND c.`end_time` <= sc.`end_time` ORDER BY sc.`id` LIMIT 1) WHERE c.`schedule_id` IS NULL AND c.`id` > 0;");
            migrationBuilder.Sql(@"UPDATE `course_schedules` c SET c.`schedule_id` = (SELECT MIN(sc.`id`) FROM `schedules` sc) WHERE c.`schedule_id` IS NULL AND c.`id` > 0;");

            migrationBuilder.AlterColumn<int>(
                name: "schedule_id",
                table: "course_schedules",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_schedules_schedule_id",
                table: "course_schedules",
                column: "schedule_id");

            migrationBuilder.AddForeignKey(
                name: "fk_course_schedules_schedules_schedule_id",
                table: "course_schedules",
                column: "schedule_id",
                principalTable: "schedules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ---- A section is the school year, the strand, the grade level and the name (11-BUSSENT-A): the schedule is no longer part of it.
            migrationBuilder.DropForeignKey(
                name: "fk_sections_schedules_schedule_id",
                table: "sections");

            migrationBuilder.DropForeignKey(
                name: "fk_sections_strand_schedules_strand_id_schedule_id",
                table: "sections");

            // Sections that were only told apart by the schedule are merged. Nothing is lost: of a group that now has the same name, the one in use stays
            // (the first when several are in use), a copy nobody uses is deleted, and another copy that students, teachers, enrollments, courses or
            // announcements use is kept under its schedule's name (for example A-PM) for the registrar to fix.
            migrationBuilder.Sql(@"CREATE TEMPORARY TABLE `tmp_section_rank` AS
SELECT u.`id`, u.`used`,
       ROW_NUMBER() OVER (PARTITION BY u.`school_year_id`, u.`strand_id`, u.`grade_level`, u.`name` ORDER BY u.`used` DESC, u.`id`) AS `place`
FROM (SELECT s.`id`, s.`school_year_id`, s.`strand_id`, s.`grade_level`, s.`name`,
             (EXISTS (SELECT 1 FROM `students` x WHERE x.`section_id` = s.`id`)
              OR EXISTS (SELECT 1 FROM `teachers` x WHERE x.`section_id` = s.`id`)
              OR EXISTS (SELECT 1 FROM `enrollments` x WHERE x.`section_id` = s.`id`)
              OR EXISTS (SELECT 1 FROM `teaching_assignments` x WHERE x.`section_id` = s.`id`)
              OR EXISTS (SELECT 1 FROM `announcements` x WHERE x.`section_id` = s.`id`)) AS `used`
      FROM `sections` s) u;");
            migrationBuilder.Sql(@"DELETE FROM `sections` WHERE `id` IN (SELECT `id` FROM `tmp_section_rank` WHERE `place` > 1 AND `used` = 0) AND `id` > 0 LIMIT 100000;");
            migrationBuilder.Sql(@"UPDATE `sections` SET `name` = CONCAT(LEFT(`name`, 44), '-', COALESCE((SELECT sc.`code` FROM `schedules` sc WHERE sc.`id` = `sections`.`schedule_id`), CAST(`id` AS CHAR)))
WHERE `id` IN (SELECT `id` FROM `tmp_section_rank` WHERE `place` > 1 AND `used` = 1) AND `id` > 0 LIMIT 100000;");
            migrationBuilder.Sql("DROP TEMPORARY TABLE `tmp_section_rank`;");

            migrationBuilder.DropTable(
                name: "strand_schedules");

            // The new indexes first: the foreign keys of the section need an index while the old ones are dropped.
            migrationBuilder.CreateIndex(
                name: "ix_sections_school_year_id_strand_id_grade_level_name",
                table: "sections",
                columns: new[] { "school_year_id", "strand_id", "grade_level", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sections_strand_id",
                table: "sections",
                column: "strand_id");

            migrationBuilder.DropIndex(
                name: "ix_sections_schedule_id",
                table: "sections");

            migrationBuilder.DropIndex(
                name: "ix_sections_school_year_id_strand_id_grade_level_schedule_id_na",
                table: "sections");

            migrationBuilder.DropIndex(
                name: "ix_sections_strand_id_schedule_id",
                table: "sections");

            migrationBuilder.DropColumn(
                name: "schedule_id",
                table: "sections");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Going back restores the columns and tables; the schedule of a section and the strand schedules were not kept, so they come back empty.
            migrationBuilder.DropForeignKey(
                name: "fk_course_schedules_schedules_schedule_id",
                table: "course_schedules");

            migrationBuilder.DropIndex(
                name: "ix_course_schedules_schedule_id",
                table: "course_schedules");

            migrationBuilder.DropColumn(
                name: "schedule_id",
                table: "course_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subjects_units",
                table: "subjects");

            migrationBuilder.DropColumn(
                name: "units",
                table: "subjects");

            migrationBuilder.DropColumn(
                name: "name_extension",
                table: "enrollments");

            migrationBuilder.AddColumn<int>(
                name: "schedule_id",
                table: "sections",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "strand_schedules",
                columns: table => new
                {
                    strand_id = table.Column<int>(type: "int", nullable: false),
                    schedule_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_strand_schedules", x => new { x.strand_id, x.schedule_id });
                    table.ForeignKey(
                        name: "fk_strand_schedules_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalTable: "schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_strand_schedules_strands_strand_id",
                        column: x => x.strand_id,
                        principalTable: "strands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_sections_schedule_id",
                table: "sections",
                column: "schedule_id");

            migrationBuilder.CreateIndex(
                name: "ix_sections_school_year_id_strand_id_grade_level_schedule_id_na",
                table: "sections",
                columns: new[] { "school_year_id", "strand_id", "grade_level", "schedule_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sections_strand_id_schedule_id",
                table: "sections",
                columns: new[] { "strand_id", "schedule_id" });

            migrationBuilder.CreateIndex(
                name: "ix_strand_schedules_schedule_id",
                table: "strand_schedules",
                column: "schedule_id");

            // The new indexes go last: the foreign keys of the section need an index all the time.
            migrationBuilder.DropIndex(
                name: "ix_sections_school_year_id_strand_id_grade_level_name",
                table: "sections");

            migrationBuilder.DropIndex(
                name: "ix_sections_strand_id",
                table: "sections");

            migrationBuilder.AddForeignKey(
                name: "fk_sections_schedules_schedule_id",
                table: "sections",
                column: "schedule_id",
                principalTable: "schedules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sections_strand_schedules_strand_id_schedule_id",
                table: "sections",
                columns: new[] { "strand_id", "schedule_id" },
                principalTable: "strand_schedules",
                principalColumns: new[] { "strand_id", "schedule_id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
