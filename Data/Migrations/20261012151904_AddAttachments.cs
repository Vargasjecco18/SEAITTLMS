using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_by_user_id = table.Column<int>(type: "int", nullable: true),
                    material_module_id = table.Column<int>(type: "int", nullable: true),
                    material_item_id = table.Column<int>(type: "int", nullable: true),
                    module_id = table.Column<int>(type: "int", nullable: true),
                    item_id = table.Column<int>(type: "int", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                    table.CheckConstraint("ck_attachments_size", "size_bytes >= 0");
                    table.ForeignKey(
                        name: "fk_attachments_material_items_material_item_id",
                        column: x => x.material_item_id,
                        principalTable: "material_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attachments_material_modules_material_module_id",
                        column: x => x.material_module_id,
                        principalTable: "material_modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attachments_module_items_item_id",
                        column: x => x.item_id,
                        principalTable: "module_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attachments_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_attachments_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "attachment_contents",
                columns: table => new
                {
                    attachment_id = table.Column<int>(type: "int", nullable: false),
                    data = table.Column<byte[]>(type: "longblob", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachment_contents", x => x.attachment_id);
                    table.ForeignKey(
                        name: "fk_attachment_contents_attachments_attachment_id",
                        column: x => x.attachment_id,
                        principalTable: "attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_item_id",
                table: "attachments",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_material_item_id",
                table: "attachments",
                column: "material_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_material_module_id",
                table: "attachments",
                column: "material_module_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_module_id",
                table: "attachments",
                column: "module_id");

            migrationBuilder.CreateIndex(
                name: "ix_attachments_uploaded_by_user_id",
                table: "attachments",
                column: "uploaded_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachment_contents");

            migrationBuilder.DropTable(
                name: "attachments");
        }
    }
}
