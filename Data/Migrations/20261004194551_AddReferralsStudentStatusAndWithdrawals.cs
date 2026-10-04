using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralsStudentStatusAndWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "students",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Active")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "status_changed_at_utc",
                table: "students",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_note",
                table: "students",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "referrals",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    assignment_id = table.Column<int>(type: "int", nullable: true),
                    reason = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    detail = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    metric = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    status = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    intervention = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    outcome = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    resolved_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    handled_by_user_id = table.Column<int>(type: "int", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_referrals", x => x.id);
                    table.CheckConstraint("ck_referrals_outcome", "outcome IS NULL OR outcome IN ('Improved', 'NoChange')");
                    table.CheckConstraint("ck_referrals_reason", "reason IN ('FailingGrades', 'AtRisk', 'LowAttendance')");
                    table.CheckConstraint("ck_referrals_status", "status IN ('Open', 'InProgress', 'Resolved')");
                    table.ForeignKey(
                        name: "fk_referrals_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_referrals_teaching_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalTable: "teaching_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_referrals_users_handled_by_user_id",
                        column: x => x.handled_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "withdrawal_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    decided_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    decided_by_user_id = table.Column<int>(type: "int", nullable: true),
                    decision_note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_withdrawal_requests", x => x.id);
                    table.CheckConstraint("ck_withdrawal_requests_status", "status IN ('Pending', 'Approved', 'Declined', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_withdrawal_requests_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_withdrawal_requests_users_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_students_status",
                table: "students",
                column: "status");

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_status",
                table: "students",
                sql: "status IN ('Active', 'Graduated', 'Withdrawn')");

            migrationBuilder.CreateIndex(
                name: "ix_referrals_assignment_id",
                table: "referrals",
                column: "assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_referrals_handled_by_user_id",
                table: "referrals",
                column: "handled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_referrals_status_reason",
                table: "referrals",
                columns: new[] { "status", "reason" });

            migrationBuilder.CreateIndex(
                name: "ix_referrals_student_id_status",
                table: "referrals",
                columns: new[] { "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_withdrawal_requests_decided_by_user_id",
                table: "withdrawal_requests",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_withdrawal_requests_status_student_id",
                table: "withdrawal_requests",
                columns: new[] { "status", "student_id" });

            migrationBuilder.CreateIndex(
                name: "ix_withdrawal_requests_student_id",
                table: "withdrawal_requests",
                column: "student_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "referrals");

            migrationBuilder.DropTable(
                name: "withdrawal_requests");

            migrationBuilder.DropIndex(
                name: "ix_students_status",
                table: "students");

            migrationBuilder.DropCheckConstraint(
                name: "ck_students_status",
                table: "students");

            migrationBuilder.DropColumn(
                name: "status",
                table: "students");

            migrationBuilder.DropColumn(
                name: "status_changed_at_utc",
                table: "students");

            migrationBuilder.DropColumn(
                name: "status_note",
                table: "students");
        }
    }
}
