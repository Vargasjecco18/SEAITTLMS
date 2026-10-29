using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class SingleSessionExamAndNotifyEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items");

            migrationBuilder.AddColumn<string>(
                name: "active_session_id",
                table: "users",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "active_session_seen_utc",
                table: "users",
                type: "datetime(6)",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items");

            migrationBuilder.DropColumn(
                name: "active_session_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "active_session_seen_utc",
                table: "users");

            migrationBuilder.DropColumn(
                name: "notify_email",
                table: "users");

            migrationBuilder.AddCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items",
                sql: "kind IN ('Activity', 'Quiz', 'TaskPerformance')");
        }
    }
}
