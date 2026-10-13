using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalGradesAndSubmissionFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "submission_id",
                table: "attachments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "final_grades",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    assignment_id = table.Column<int>(type: "int", nullable: false),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    grade = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_final_grades", x => x.id);
                    table.CheckConstraint("ck_final_grades_grade", "grade >= 0 AND grade <= 100");
                    table.ForeignKey(
                        name: "fk_final_grades_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_final_grades_teaching_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalTable: "teaching_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_submission_id",
                table: "attachments",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ix_final_grades_assignment_id_student_id",
                table: "final_grades",
                columns: new[] { "assignment_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_final_grades_student_id",
                table: "final_grades",
                column: "student_id");

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_submissions_submission_id",
                table: "attachments",
                column: "submission_id",
                principalTable: "submissions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attachments_submissions_submission_id",
                table: "attachments");

            migrationBuilder.DropTable(
                name: "final_grades");

            migrationBuilder.DropIndex(
                name: "ix_attachments_submission_id",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "submission_id",
                table: "attachments");
        }
    }
}
