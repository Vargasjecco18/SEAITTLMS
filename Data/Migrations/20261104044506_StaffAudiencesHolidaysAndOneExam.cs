using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaffAudiencesHolidaysAndOneExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_announcements_audience",
                table: "announcements");

            migrationBuilder.AlterColumn<string>(
                name: "audience",
                table: "announcements",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "holiday_exclusions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_holiday_exclusions", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_announcements_audience",
                table: "announcements",
                sql: "audience IN ('Everyone', 'Students', 'Teachers', 'Staff', 'StaffTeachers')");

            migrationBuilder.CreateIndex(
                name: "ix_holiday_exclusions_name",
                table: "holiday_exclusions",
                column: "name",
                unique: true);

            // Each course has ONE summative exam: the practice "Summative exam 2" of every course goes (with its questions, and the copy that was posted to the sections of the teachers).
            migrationBuilder.Sql(
                "DELETE FROM `module_items` WHERE `id` > 0 AND `material_item_id` IN (SELECT `id` FROM (SELECT `id` FROM `material_items` WHERE `kind` = 'Exam' AND `title` = 'Summative exam 2') x);");
            migrationBuilder.Sql(
                "DELETE FROM `material_items` WHERE `id` > 0 AND `kind` = 'Exam' AND `title` = 'Summative exam 2';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "holiday_exclusions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_announcements_audience",
                table: "announcements");

            migrationBuilder.AlterColumn<string>(
                name: "audience",
                table: "announcements",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_announcements_audience",
                table: "announcements",
                sql: "audience IN ('Everyone', 'Students', 'Teachers')");
        }
    }
}
