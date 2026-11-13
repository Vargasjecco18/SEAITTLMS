using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class LoginOtps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "login_otps",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    challenge_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    code_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    sends = table.Column<int>(type: "int", nullable: false),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    sent_to = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_login_otps", x => x.id);
                    table.ForeignKey(
                        name: "fk_login_otps_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_login_otps_challenge_hash",
                table: "login_otps",
                column: "challenge_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_login_otps_expires_at_utc",
                table: "login_otps",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_login_otps_user_id",
                table: "login_otps",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_otps");
        }
    }
}
