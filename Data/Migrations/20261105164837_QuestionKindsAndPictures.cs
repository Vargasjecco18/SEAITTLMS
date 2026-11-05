using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuestionKindsAndPictures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_quiz_questions_kind",
                table: "quiz_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_material_questions_kind",
                table: "material_questions");

            migrationBuilder.AlterColumn<string>(
                name: "correct_answer",
                table: "quiz_questions",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "correct_answer",
                table: "material_questions",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldMaxLength: 500)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "material_question_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    question_id = table.Column<int>(type: "int", nullable: false),
                    slot = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    data = table.Column<byte[]>(type: "longblob", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_question_images", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_question_images_material_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "material_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "quiz_question_images",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    question_id = table.Column<int>(type: "int", nullable: false),
                    slot = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    data = table.Column<byte[]>(type: "longblob", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_question_images", x => x.id);
                    table.ForeignKey(
                        name: "fk_quiz_question_images_quiz_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "quiz_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quiz_questions_kind",
                table: "quiz_questions",
                sql: "kind IN ('MultipleChoice', 'TrueFalse', 'ShortAnswer', 'FillInTheBlank', 'Matching', 'Identification', 'MultipleSelect', 'Essay')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_material_questions_kind",
                table: "material_questions",
                sql: "kind IN ('MultipleChoice', 'TrueFalse', 'ShortAnswer', 'FillInTheBlank', 'Matching', 'Identification', 'MultipleSelect', 'Essay')");

            migrationBuilder.CreateIndex(
                name: "ix_material_question_images_question_id_slot",
                table: "material_question_images",
                columns: new[] { "question_id", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quiz_question_images_question_id_slot",
                table: "quiz_question_images",
                columns: new[] { "question_id", "slot" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "material_question_images");

            migrationBuilder.DropTable(
                name: "quiz_question_images");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quiz_questions_kind",
                table: "quiz_questions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_material_questions_kind",
                table: "material_questions");

            migrationBuilder.AlterColumn<string>(
                name: "correct_answer",
                table: "quiz_questions",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(2000)",
                oldMaxLength: 2000)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "correct_answer",
                table: "material_questions",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(2000)",
                oldMaxLength: 2000)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quiz_questions_kind",
                table: "quiz_questions",
                sql: "kind IN ('MultipleChoice', 'TrueFalse', 'ShortAnswer')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_material_questions_kind",
                table: "material_questions",
                sql: "kind IN ('MultipleChoice', 'TrueFalse', 'ShortAnswer')");
        }
    }
}
