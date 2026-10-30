using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomStaffRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "custom_role_id",
                table: "users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "custom_roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    base_role = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_roles", x => x.id);
                    table.CheckConstraint("ck_custom_roles_base", "base_role IN ('Registrar', 'GuidanceCounselor', 'AcademicCoordinator', 'AcademicProvider')");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_users_custom_role_id",
                table: "users",
                column: "custom_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_roles_name",
                table: "custom_roles",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_users_custom_roles_custom_role_id",
                table: "users",
                column: "custom_role_id",
                principalTable: "custom_roles",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_custom_roles_custom_role_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "custom_roles");

            migrationBuilder.DropIndex(
                name: "ix_users_custom_role_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "custom_role_id",
                table: "users");
        }
    }
}
