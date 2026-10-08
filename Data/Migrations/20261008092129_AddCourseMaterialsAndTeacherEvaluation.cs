using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseMaterialsAndTeacherEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_syllabus",
                table: "modules",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "material_modules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    subject_id = table.Column<int>(type: "int", nullable: false),
                    is_syllabus = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    body = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    link_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    position = table.Column<int>(type: "int", nullable: false),
                    created_by_user_id = table.Column<int>(type: "int", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_modules", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_modules_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_material_modules_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "teacher_evaluations",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    instructions = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sent_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    due_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    term_id = table.Column<int>(type: "int", nullable: true),
                    created_by_user_id = table.Column<int>(type: "int", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teacher_evaluations", x => x.id);
                    table.ForeignKey(
                        name: "fk_teacher_evaluations_terms_term_id",
                        column: x => x.term_id,
                        principalTable: "terms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_teacher_evaluations_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "material_deliveries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    material_module_id = table.Column<int>(type: "int", nullable: false),
                    assignment_id = table.Column<int>(type: "int", nullable: false),
                    module_id = table.Column<int>(type: "int", nullable: false),
                    sent_by_user_id = table.Column<int>(type: "int", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_deliveries_material_modules_material_module_id",
                        column: x => x.material_module_id,
                        principalTable: "material_modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_material_deliveries_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_material_deliveries_teaching_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalTable: "teaching_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_material_deliveries_users_sent_by_user_id",
                        column: x => x.sent_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "material_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    module_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    instructions = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    max_score = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    due_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    position = table.Column<int>(type: "int", nullable: false),
                    time_limit_minutes = table.Column<int>(type: "int", nullable: true),
                    shuffle_questions = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    shuffle_options = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    anti_cheat = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    max_violations = table.Column<int>(type: "int", nullable: false),
                    show_result = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_items", x => x.id);
                    table.CheckConstraint("ck_material_items_kind", "kind IN ('Activity', 'Quiz', 'TaskPerformance')");
                    table.CheckConstraint("ck_material_items_max_score", "max_score > 0");
                    table.ForeignKey(
                        name: "fk_material_items_material_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "material_modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "evaluation_questions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    evaluation_id = table.Column<int>(type: "int", nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    text = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    kind = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    category = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_questions", x => x.id);
                    table.CheckConstraint("ck_evaluation_questions_kind", "kind IN ('Rating', 'Comment')");
                    table.ForeignKey(
                        name: "fk_evaluation_questions_teacher_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "teacher_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "evaluation_responses",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    evaluation_id = table.Column<int>(type: "int", nullable: false),
                    assignment_id = table.Column<int>(type: "int", nullable: false),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_responses", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_responses_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_evaluation_responses_teacher_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "teacher_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_evaluation_responses_teaching_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalTable: "teaching_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "evaluation_targets",
                columns: table => new
                {
                    evaluation_id = table.Column<int>(type: "int", nullable: false),
                    assignment_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_targets", x => new { x.evaluation_id, x.assignment_id });
                    table.ForeignKey(
                        name: "fk_evaluation_targets_teacher_evaluations_evaluation_id",
                        column: x => x.evaluation_id,
                        principalTable: "teacher_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_evaluation_targets_teaching_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalTable: "teaching_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "material_questions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    item_id = table.Column<int>(type: "int", nullable: false),
                    position = table.Column<int>(type: "int", nullable: false),
                    text = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    options_text = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    correct_answer = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    points = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_questions", x => x.id);
                    table.CheckConstraint("ck_material_questions_kind", "kind IN ('MultipleChoice', 'TrueFalse', 'ShortAnswer')");
                    table.ForeignKey(
                        name: "fk_material_questions_material_items_item_id",
                        column: x => x.item_id,
                        principalTable: "material_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "evaluation_answers",
                columns: table => new
                {
                    response_id = table.Column<int>(type: "int", nullable: false),
                    question_id = table.Column<int>(type: "int", nullable: false),
                    rating = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                    comment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_answers", x => new { x.response_id, x.question_id });
                    table.CheckConstraint("ck_evaluation_answers_rating", "rating IS NULL OR rating BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_evaluation_answers_evaluation_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "evaluation_questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_evaluation_answers_evaluation_responses_response_id",
                        column: x => x.response_id,
                        principalTable: "evaluation_responses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_answers_question_id",
                table: "evaluation_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_questions_evaluation_id_position",
                table: "evaluation_questions",
                columns: new[] { "evaluation_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_responses_assignment_id",
                table: "evaluation_responses",
                column: "assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_responses_evaluation_id_assignment_id_student_id",
                table: "evaluation_responses",
                columns: new[] { "evaluation_id", "assignment_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_responses_student_id_evaluation_id",
                table: "evaluation_responses",
                columns: new[] { "student_id", "evaluation_id" });

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_targets_assignment_id",
                table: "evaluation_targets",
                column: "assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_deliveries_assignment_id",
                table: "material_deliveries",
                column: "assignment_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_deliveries_material_module_id_assignment_id",
                table: "material_deliveries",
                columns: new[] { "material_module_id", "assignment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_deliveries_module_id",
                table: "material_deliveries",
                column: "module_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_deliveries_sent_by_user_id",
                table: "material_deliveries",
                column: "sent_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_items_module_id_position",
                table: "material_items",
                columns: new[] { "module_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_material_modules_created_by_user_id",
                table: "material_modules",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_modules_subject_id_position",
                table: "material_modules",
                columns: new[] { "subject_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_material_questions_item_id_position",
                table: "material_questions",
                columns: new[] { "item_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_teacher_evaluations_created_by_user_id",
                table: "teacher_evaluations",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_evaluations_sent_at_utc",
                table: "teacher_evaluations",
                column: "sent_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_evaluations_term_id",
                table: "teacher_evaluations",
                column: "term_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_answers");

            migrationBuilder.DropTable(
                name: "evaluation_targets");

            migrationBuilder.DropTable(
                name: "material_deliveries");

            migrationBuilder.DropTable(
                name: "material_questions");

            migrationBuilder.DropTable(
                name: "evaluation_questions");

            migrationBuilder.DropTable(
                name: "evaluation_responses");

            migrationBuilder.DropTable(
                name: "material_items");

            migrationBuilder.DropTable(
                name: "teacher_evaluations");

            migrationBuilder.DropTable(
                name: "material_modules");

            migrationBuilder.DropColumn(
                name: "is_syllabus",
                table: "modules");
        }
    }
}
