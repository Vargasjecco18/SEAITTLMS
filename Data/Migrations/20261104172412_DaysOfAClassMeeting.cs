using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class DaysOfAClassMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The new unique index is made first: the foreign key of section_id needs an index that starts with it, so the old one can only be dropped after.
            migrationBuilder.DropCheckConstraint(
                name: "ck_course_schedules_days",
                table: "course_schedules");

            migrationBuilder.AlterColumn<string>(
                name: "days",
                table: "course_schedules",
                type: "varchar(6)",
                maxLength: 6,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(3)",
                oldMaxLength: 3)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_course_schedules_section_id_subject_id_days",
                table: "course_schedules",
                columns: new[] { "section_id", "subject_id", "days" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "ix_course_schedules_section_id_subject_id",
                table: "course_schedules");

            migrationBuilder.AddCheckConstraint(
                name: "ck_course_schedules_days",
                table: "course_schedules",
                sql: "days REGEXP '^M?T?W?(TH)?F?$' AND days <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_course_schedules_days",
                table: "course_schedules");

            // A course had one row again: the first of its rows stays (and gets the pattern of its first day), the other groups of days go.
            migrationBuilder.Sql("DELETE c FROM course_schedules c JOIN (SELECT section_id, subject_id, MIN(id) AS keep FROM course_schedules GROUP BY section_id, subject_id) k ON k.section_id = c.section_id AND k.subject_id = c.subject_id WHERE c.id <> k.keep;");
            migrationBuilder.Sql("UPDATE course_schedules SET days = CASE WHEN days LIKE 'M%' OR days LIKE 'W%' OR days = 'F' THEN 'MWF' ELSE 'TTH' END WHERE id > 0;");

            migrationBuilder.CreateIndex(
                name: "ix_course_schedules_section_id_subject_id",
                table: "course_schedules",
                columns: new[] { "section_id", "subject_id" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "ix_course_schedules_section_id_subject_id_days",
                table: "course_schedules");

            migrationBuilder.AlterColumn<string>(
                name: "days",
                table: "course_schedules",
                type: "varchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(6)",
                oldMaxLength: 6)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_course_schedules_days",
                table: "course_schedules",
                sql: "days IN ('MWF', 'TTH')");
        }
    }
}
