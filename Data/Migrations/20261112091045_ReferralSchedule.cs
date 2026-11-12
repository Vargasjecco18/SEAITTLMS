using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReferralSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "scheduled_at_utc",
                table: "referrals",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "student_responded_at_utc",
                table: "referrals",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "student_response",
                table: "referrals",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "student_seen_at_utc",
                table: "referrals",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_referrals_student_response",
                table: "referrals",
                sql: "student_response IS NULL OR student_response IN ('Going', 'NotGoing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_referrals_student_response",
                table: "referrals");

            migrationBuilder.DropColumn(
                name: "scheduled_at_utc",
                table: "referrals");

            migrationBuilder.DropColumn(
                name: "student_responded_at_utc",
                table: "referrals");

            migrationBuilder.DropColumn(
                name: "student_response",
                table: "referrals");

            migrationBuilder.DropColumn(
                name: "student_seen_at_utc",
                table: "referrals");
        }
    }
}
