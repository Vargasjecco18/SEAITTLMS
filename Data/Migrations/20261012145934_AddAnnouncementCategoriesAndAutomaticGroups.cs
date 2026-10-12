using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <summary>
    /// Announcements get a category (exam, holiday, semester break, teachers' meeting, other) and the course a teacher wrote them for.
    /// Groups can now be made by the system (grade, course and everybody groups), so a group no longer needs an owner.
    /// </summary>
    public partial class AddAnnouncementCategoriesAndAutomaticGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "owner_user_id",
                table: "study_groups",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "auto_key",
                table: "study_groups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "auto_kind",
                table: "study_groups",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "assignment_id",
                table: "announcements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "announcements",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Other")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_study_groups_auto_kind_auto_key",
                table: "study_groups",
                columns: new[] { "auto_kind", "auto_key" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_groups_auto_kind",
                table: "study_groups",
                sql: "auto_kind IS NULL OR auto_kind IN ('GradeStudents', 'CourseStudents', 'AllStudents', 'GradeTeachers', 'AllTeachers')");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_assignment_id",
                table: "announcements",
                column: "assignment_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_announcements_category",
                table: "announcements",
                sql: "category IN ('Exam', 'Holiday', 'SemesterBreak', 'TeachersMeeting', 'Other')");

            migrationBuilder.AddForeignKey(
                name: "fk_announcements_teaching_assignments_assignment_id",
                table: "announcements",
                column: "assignment_id",
                principalTable: "teaching_assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The automatic groups have no owner, so they can't stay in a database where every group needs one. (The groups people made stay.)
            migrationBuilder.Sql("DELETE FROM `study_groups` WHERE `id` > 0 AND `auto_kind` IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "fk_announcements_teaching_assignments_assignment_id",
                table: "announcements");

            migrationBuilder.DropIndex(
                name: "ix_study_groups_auto_kind_auto_key",
                table: "study_groups");

            migrationBuilder.DropCheckConstraint(
                name: "ck_study_groups_auto_kind",
                table: "study_groups");

            migrationBuilder.DropIndex(
                name: "ix_announcements_assignment_id",
                table: "announcements");

            migrationBuilder.DropCheckConstraint(
                name: "ck_announcements_category",
                table: "announcements");

            migrationBuilder.DropColumn(
                name: "auto_key",
                table: "study_groups");

            migrationBuilder.DropColumn(
                name: "auto_kind",
                table: "study_groups");

            migrationBuilder.DropColumn(
                name: "assignment_id",
                table: "announcements");

            migrationBuilder.DropColumn(
                name: "category",
                table: "announcements");

            migrationBuilder.AlterColumn<int>(
                name: "owner_user_id",
                table: "study_groups",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
