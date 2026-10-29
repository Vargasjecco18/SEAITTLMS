using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class MessageAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "message_id",
                table: "attachments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_attachments_message_id",
                table: "attachments",
                column: "message_id");

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_messages_message_id",
                table: "attachments",
                column: "message_id",
                principalTable: "messages",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attachments_messages_message_id",
                table: "attachments");

            migrationBuilder.DropIndex(
                name: "ix_attachments_message_id",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "message_id",
                table: "attachments");
        }
    }
}
