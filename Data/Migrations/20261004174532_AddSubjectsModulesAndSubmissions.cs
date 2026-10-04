using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectsModulesAndSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teaching_assignments",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    teacher_id = table.Column<int>(type: "int", nullable: false),
                    section_id = table.Column<int>(type: "int", nullable: false),
                    subject_id = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teaching_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_teaching_assignments_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teaching_assignments_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teaching_assignments_teachers_teacher_id",
                        column: x => x.teacher_id,
                        principalTable: "teachers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "modules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    assignment_id = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    body = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    link_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    position = table.Column<int>(type: "int", nullable: false),
                    visible_from_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    notified_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modules", x => x.id);
                    table.ForeignKey(
                        name: "fk_modules_teaching_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalTable: "teaching_assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "module_items",
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
                    visible_from_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    notified_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    due_reminder_sent_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("pk_module_items", x => x.id);
                    table.CheckConstraint("ck_module_items_kind", "kind IN ('Activity', 'Quiz', 'TaskPerformance')");
                    table.CheckConstraint("ck_module_items_max_score", "max_score > 0");
                    table.ForeignKey(
                        name: "fk_module_items_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "module_views",
                columns: table => new
                {
                    module_id = table.Column<int>(type: "int", nullable: false),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    first_opened_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_module_views", x => new { x.module_id, x.student_id });
                    table.ForeignKey(
                        name: "fk_module_views_modules_module_id",
                        column: x => x.module_id,
                        principalTable: "modules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_module_views_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "quiz_questions",
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
                    table.PrimaryKey("pk_quiz_questions", x => x.id);
                    table.CheckConstraint("ck_quiz_questions_kind", "kind IN ('MultipleChoice', 'TrueFalse', 'ShortAnswer')");
                    table.ForeignKey(
                        name: "fk_quiz_questions_module_items_item_id",
                        column: x => x.item_id,
                        principalTable: "module_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    item_id = table.Column<int>(type: "int", nullable: false),
                    student_id = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    started_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    submitted_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    is_late = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    answer_text = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    link_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    answers_json = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    score = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    feedback = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    graded_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    violation_count = table.Column<int>(type: "int", nullable: false),
                    violation_log = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_flagged = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    auto_submit_reason = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submissions", x => x.id);
                    table.CheckConstraint("ck_submissions_score", "score IS NULL OR score >= 0");
                    table.CheckConstraint("ck_submissions_status", "status IN ('InProgress', 'Submitted', 'Graded')");
                    table.ForeignKey(
                        name: "fk_submissions_module_items_item_id",
                        column: x => x.item_id,
                        principalTable: "module_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_submissions_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_module_items_due_at_utc",
                table: "module_items",
                column: "due_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_module_items_module_id_kind_position",
                table: "module_items",
                columns: new[] { "module_id", "kind", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_module_views_student_id",
                table: "module_views",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_modules_assignment_id_position",
                table: "modules",
                columns: new[] { "assignment_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_modules_visible_from_utc",
                table: "modules",
                column: "visible_from_utc");

            migrationBuilder.CreateIndex(
                name: "ix_quiz_questions_item_id_position",
                table: "quiz_questions",
                columns: new[] { "item_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_submissions_item_id_student_id",
                table: "submissions",
                columns: new[] { "item_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submissions_student_id_status",
                table: "submissions",
                columns: new[] { "student_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_teaching_assignments_section_id_subject_id",
                table: "teaching_assignments",
                columns: new[] { "section_id", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teaching_assignments_subject_id",
                table: "teaching_assignments",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_teaching_assignments_teacher_id",
                table: "teaching_assignments",
                column: "teacher_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "module_views");

            migrationBuilder.DropTable(
                name: "quiz_questions");

            migrationBuilder.DropTable(
                name: "submissions");

            migrationBuilder.DropTable(
                name: "module_items");

            migrationBuilder.DropTable(
                name: "modules");

            migrationBuilder.DropTable(
                name: "teaching_assignments");
        }
    }
}
