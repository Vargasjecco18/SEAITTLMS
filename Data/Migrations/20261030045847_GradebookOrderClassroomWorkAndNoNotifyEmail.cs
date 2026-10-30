using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class GradebookOrderClassroomWorkAndNoNotifyEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items");

            migrationBuilder.DropColumn(
                name: "notify_email",
                table: "users");

            migrationBuilder.AddColumn<int>(
                name: "gradebook_order",
                table: "module_items",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items",
                sql: "kind IN ('Activity', 'Quiz', 'TaskPerformance', 'Seatwork', 'Recitation', 'Exam')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items");

            migrationBuilder.DropColumn(
                name: "gradebook_order",
                table: "module_items");

            migrationBuilder.AddColumn<string>(
                name: "notify_email",
                table: "users",
                type: "varchar(254)",
                maxLength: 254,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items",
                sql: "kind IN ('Activity', 'Quiz', 'TaskPerformance', 'Exam')");
        }
    }
}
