using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeekdayAttendanceAndIgnoredReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Classes are held from Monday to Friday: the marks that were saved on a Saturday or a Sunday are taken out before the check (WEEKDAY: 0 = Monday ... 6 = Sunday).
            migrationBuilder.Sql("DELETE FROM `attendance_records` WHERE `id` > 0 AND WEEKDAY(`attended_on`) >= 5;");

            migrationBuilder.AddColumn<DateTime>(
                name: "student_ignored_at_utc",
                table: "referrals",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_attendance_records_weekday",
                table: "attendance_records",
                sql: "WEEKDAY(attended_on) < 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_attendance_records_weekday",
                table: "attendance_records");

            migrationBuilder.DropColumn(
                name: "student_ignored_at_utc",
                table: "referrals");
        }
    }
}
