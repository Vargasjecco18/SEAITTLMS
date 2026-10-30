using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class SummativeExamsAndConcerns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_referrals_reason",
                table: "referrals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_material_items_kind",
                table: "material_items");

            migrationBuilder.AddColumn<int>(
                name: "material_item_id",
                table: "module_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_exam_set",
                table: "material_modules",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "visible_from_utc",
                table: "material_items",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_referrals_reason",
                table: "referrals",
                sql: "reason IN ('FailingGrades', 'AtRisk', 'LowAttendance', 'Violation')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_material_items_kind",
                table: "material_items",
                sql: "kind IN ('Activity', 'Quiz', 'TaskPerformance', 'Seatwork', 'Recitation', 'Exam')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_referrals_reason",
                table: "referrals");

            migrationBuilder.DropCheckConstraint(
                name: "ck_material_items_kind",
                table: "material_items");

            migrationBuilder.DropColumn(
                name: "material_item_id",
                table: "module_items");

            migrationBuilder.DropColumn(
                name: "is_exam_set",
                table: "material_modules");

            migrationBuilder.DropColumn(
                name: "visible_from_utc",
                table: "material_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_referrals_reason",
                table: "referrals",
                sql: "reason IN ('FailingGrades', 'AtRisk', 'LowAttendance')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_material_items_kind",
                table: "material_items",
                sql: "kind IN ('Activity', 'Quiz', 'TaskPerformance')");
        }
    }
}
