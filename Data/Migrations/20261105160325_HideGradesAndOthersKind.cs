using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class HideGradesAndOthersKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items");

            migrationBuilder.AddColumn<bool>(
                name: "grades_hidden",
                table: "teaching_assignments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items",
                sql: "kind IN ('Activity', 'Quiz', 'TaskPerformance', 'Seatwork', 'Recitation', 'Exam', 'Other')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items");

            migrationBuilder.DropColumn(
                name: "grades_hidden",
                table: "teaching_assignments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items",
                sql: "kind IN ('Activity', 'Quiz', 'TaskPerformance', 'Seatwork', 'Recitation', 'Exam')");
        }
    }
}
