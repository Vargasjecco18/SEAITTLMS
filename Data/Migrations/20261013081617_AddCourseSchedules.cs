using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "end_time",
                table: "schedules",
                type: "time(6)",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "start_time",
                table: "schedules",
                type: "time(6)",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.CreateTable(
                name: "course_schedules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    section_id = table.Column<int>(type: "int", nullable: false),
                    subject_id = table.Column<int>(type: "int", nullable: false),
                    days = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    start_time = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_schedules", x => x.id);
                    table.CheckConstraint("ck_course_schedules_days", "days IN ('MWF', 'TTH')");
                    table.CheckConstraint("ck_course_schedules_hours", "end_time > start_time");
                    table.ForeignKey(
                        name: "fk_course_schedules_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_course_schedules_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // The hours of the three parts of the day (the academic coordinator can change them in the academic setup). `id` > 0 keeps MySQL Workbench's safe update mode happy.
            migrationBuilder.Sql("UPDATE `schedules` SET `start_time` = '07:00:00', `end_time` = '12:00:00' WHERE `code` = 'AM' AND `id` > 0;");
            migrationBuilder.Sql("UPDATE `schedules` SET `start_time` = '10:00:00', `end_time` = '15:00:00' WHERE `code` = 'MID' AND `id` > 0;");
            migrationBuilder.Sql("UPDATE `schedules` SET `start_time` = '13:00:00', `end_time` = '18:00:00' WHERE `code` = 'PM' AND `id` > 0;");
            migrationBuilder.Sql("UPDATE `schedules` SET `start_time` = '07:00:00', `end_time` = '17:00:00' WHERE `start_time` = `end_time` AND `id` > 0;");

            // The columns were added with a default of 00:00 only so the rows that exist could get them; the model has no default.
            migrationBuilder.AlterColumn<TimeOnly>(
                name: "end_time",
                table: "schedules",
                type: "time(6)",
                nullable: false,
                oldClrType: typeof(TimeOnly),
                oldType: "time(6)",
                oldDefaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "start_time",
                table: "schedules",
                type: "time(6)",
                nullable: false,
                oldClrType: typeof(TimeOnly),
                oldType: "time(6)",
                oldDefaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddCheckConstraint(
                name: "ck_schedules_hours",
                table: "schedules",
                sql: "end_time > start_time");

            migrationBuilder.CreateIndex(
                name: "ix_course_schedules_section_id_subject_id",
                table: "course_schedules",
                columns: new[] { "section_id", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_schedules_subject_id",
                table: "course_schedules",
                column: "subject_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "course_schedules");

            migrationBuilder.DropCheckConstraint(
                name: "ck_schedules_hours",
                table: "schedules");

            migrationBuilder.DropColumn(
                name: "end_time",
                table: "schedules");

            migrationBuilder.DropColumn(
                name: "start_time",
                table: "schedules");
        }
    }
}
