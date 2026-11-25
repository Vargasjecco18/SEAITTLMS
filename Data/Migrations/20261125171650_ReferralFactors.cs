using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReferralFactors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_referrals_reason",
                table: "referrals");

            migrationBuilder.AddCheckConstraint(
                name: "ck_referrals_reason",
                table: "referrals",
                sql: "reason IN ('FailingGrades', 'AtRisk', 'LowAttendance', 'Violation', 'NonCompliance', 'Behavioral')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_referrals_reason",
                table: "referrals");

            migrationBuilder.AddCheckConstraint(
                name: "ck_referrals_reason",
                table: "referrals",
                sql: "reason IN ('FailingGrades', 'AtRisk', 'LowAttendance', 'Violation')");
        }
    }
}
