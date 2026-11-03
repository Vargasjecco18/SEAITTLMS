using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnnouncementDaysAndStaffGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_study_groups_auto_kind",
                table: "study_groups");

            migrationBuilder.AddColumn<DateOnly>(
                name: "starts_on",
                table: "announcements",
                type: "date",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_groups_auto_kind",
                table: "study_groups",
                sql: "auto_kind IS NULL OR auto_kind IN ('GradeStudents', 'CourseStudents', 'AllStudents', 'GradeTeachers', 'AllTeachers', 'AllStaff', 'TeachersAndStaff')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_announcements_schedule",
                table: "announcements",
                sql: "starts_on IS NULL OR expires_on IS NULL OR starts_on <= expires_on");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_study_groups_auto_kind",
                table: "study_groups");

            migrationBuilder.DropCheckConstraint(
                name: "ck_announcements_schedule",
                table: "announcements");

            migrationBuilder.DropColumn(
                name: "starts_on",
                table: "announcements");

            migrationBuilder.AddCheckConstraint(
                name: "ck_study_groups_auto_kind",
                table: "study_groups",
                sql: "auto_kind IS NULL OR auto_kind IN ('GradeStudents', 'CourseStudents', 'AllStudents', 'GradeTeachers', 'AllTeachers')");
        }
    }
}
