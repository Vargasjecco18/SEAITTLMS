using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoomsAndThreeTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "room_id",
                table: "course_schedules",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_course_schedules_room_id",
                table: "course_schedules",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_name",
                table: "rooms",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_course_schedules_rooms_room_id",
                table: "course_schedules",
                column: "room_id",
                principalTable: "rooms",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Every school year has three terms. The school year the database came with (2026-2027) had two: its 2nd term was the rest of the year, so it is shortened
            // to the end of January and a 3rd term (February and March) is added. A school year that was changed by hand is left as it is.
            migrationBuilder.Sql(
                "INSERT INTO `terms` (`school_year_id`, `term_number`, `name`, `start_date`, `end_date`, `is_current`, `created_at_utc`, `updated_at_utc`) " +
                "SELECT y.`id`, 3, '3rd Term', '2027-02-01', '2027-03-31', 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM `school_years` y " +
                "WHERE y.`name` = '2026-2027' AND y.`end_date` = '2027-03-31' " +
                "AND EXISTS (SELECT 1 FROM `terms` t WHERE t.`school_year_id` = y.`id` AND t.`term_number` = 2 AND t.`start_date` = '2026-11-02' AND t.`end_date` = '2027-03-31') " +
                "AND NOT EXISTS (SELECT 1 FROM `terms` t WHERE t.`school_year_id` = y.`id` AND t.`term_number` = 3);");
            migrationBuilder.Sql(
                "UPDATE `terms` SET `end_date` = '2027-01-29' WHERE `id` > 0 AND `term_number` = 2 AND `start_date` = '2026-11-02' AND `end_date` = '2027-03-31' " +
                "AND `school_year_id` IN (SELECT y.`id` FROM (SELECT `id` FROM `school_years` WHERE `name` = '2026-2027') y) " +
                "AND `school_year_id` IN (SELECT z.`school_year_id` FROM (SELECT `school_year_id` FROM `terms` WHERE `term_number` = 3 AND `start_date` = '2027-02-01') z);");

            // Sections: A and B only. A section C that nobody uses is removed (one with students or teachers stays).
            migrationBuilder.Sql(
                "DELETE FROM `sections` WHERE `id` > 0 AND `name` = 'C' " +
                "AND NOT EXISTS (SELECT 1 FROM `enrollments` e WHERE e.`section_id` = `sections`.`id`) " +
                "AND NOT EXISTS (SELECT 1 FROM `teaching_assignments` a WHERE a.`section_id` = `sections`.`id`) " +
                "AND NOT EXISTS (SELECT 1 FROM `students` x WHERE x.`section_id` = `sections`.`id`) " +
                "AND NOT EXISTS (SELECT 1 FROM `teachers` x WHERE x.`section_id` = `sections`.`id`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_course_schedules_rooms_room_id",
                table: "course_schedules");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropIndex(
                name: "ix_course_schedules_room_id",
                table: "course_schedules");

            migrationBuilder.DropColumn(
                name: "room_id",
                table: "course_schedules");
        }
    }
}
