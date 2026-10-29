using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class StudentDroppedOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_students_status",
                table: "students");

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_status",
                table: "students",
                sql: "status IN ('Active', 'Graduated', 'DroppedOut')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_students_status",
                table: "students");

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_status",
                table: "students",
                sql: "status IN ('Active', 'Graduated')");
        }
    }
}
