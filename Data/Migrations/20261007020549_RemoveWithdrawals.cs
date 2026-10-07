using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "withdrawal_requests");

            migrationBuilder.DropCheckConstraint(
                name: "ck_students_status",
                table: "students");

            // A student who was withdrawn is a student at school again (the sign-in stays switched off until an
            // administrator turns it on in Users Management). "id > 0" keeps the statement allowed in MySQL Workbench's
            // safe update mode, which wants a key in the WHERE.
            migrationBuilder.Sql("UPDATE students SET status = 'Active', status_note = NULL WHERE id > 0 AND status = 'Withdrawn';");

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_status",
                table: "students",
                sql: "status IN ('Active', 'Graduated')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_students_status",
                table: "students");

            migrationBuilder.CreateTable(
                name: "withdrawal_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    decided_by_user_id = table.Column<int>(type: "int", nullable: true),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    decided_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    decision_note = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
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

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_status",
                table: "students",
                sql: "status IN ('Active', 'Graduated', 'Withdrawn')");

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
    }
}
