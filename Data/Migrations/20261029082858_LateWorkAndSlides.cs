using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class LateWorkAndSlides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "late_decision",
                table: "submissions",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "is_slides",
                table: "attachments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_submissions_late_decision",
                table: "submissions",
                sql: "late_decision IS NULL OR late_decision IN ('Pending', 'Accepted', 'Declined')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_submissions_late_decision",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "late_decision",
                table: "submissions");

            migrationBuilder.DropColumn(
                name: "is_slides",
                table: "attachments");
        }
    }
}
