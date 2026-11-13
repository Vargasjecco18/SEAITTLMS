using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameModulesToHandouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attachments_material_modules_material_module_id",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "fk_attachments_module_items_item_id",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "fk_attachments_modules_module_id",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "fk_item_views_module_items_item_id",
                table: "item_views");

            migrationBuilder.DropForeignKey(
                name: "fk_material_deliveries_material_modules_material_module_id",
                table: "material_deliveries");

            migrationBuilder.DropForeignKey(
                name: "fk_material_deliveries_modules_module_id",
                table: "material_deliveries");

            migrationBuilder.DropForeignKey(
                name: "fk_material_items_material_modules_module_id",
                table: "material_items");

            migrationBuilder.DropForeignKey(
                name: "fk_material_modules_courses_course_id",
                table: "material_modules");

            migrationBuilder.DropForeignKey(
                name: "fk_material_modules_users_created_by_user_id",
                table: "material_modules");

            migrationBuilder.DropForeignKey(
                name: "fk_module_items_modules_module_id",
                table: "module_items");

            migrationBuilder.DropForeignKey(
                name: "fk_module_reads_modules_module_id",
                table: "module_reads");

            migrationBuilder.DropForeignKey(
                name: "fk_module_reads_students_student_id",
                table: "module_reads");

            migrationBuilder.DropForeignKey(
                name: "fk_module_views_modules_module_id",
                table: "module_views");

            migrationBuilder.DropForeignKey(
                name: "fk_module_views_students_student_id",
                table: "module_views");

            migrationBuilder.DropForeignKey(
                name: "fk_modules_teaching_assignments_assignment_id",
                table: "modules");

            migrationBuilder.DropForeignKey(
                name: "fk_quiz_questions_module_items_item_id",
                table: "quiz_questions");

            migrationBuilder.DropForeignKey(
                name: "fk_submissions_module_items_item_id",
                table: "submissions");

            migrationBuilder.DropPrimaryKey(
                name: "pk_modules",
                table: "modules");

            migrationBuilder.DropPrimaryKey(
                name: "pk_module_views",
                table: "module_views");

            migrationBuilder.DropPrimaryKey(
                name: "pk_module_reads",
                table: "module_reads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_module_reads_percent",
                table: "module_reads");

            migrationBuilder.DropPrimaryKey(
                name: "pk_module_items",
                table: "module_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_module_items_max_score",
                table: "module_items");

            migrationBuilder.DropPrimaryKey(
                name: "pk_material_modules",
                table: "material_modules");

            migrationBuilder.RenameTable(
                name: "modules",
                newName: "handouts");

            migrationBuilder.RenameTable(
                name: "module_views",
                newName: "handout_views");

            migrationBuilder.RenameTable(
                name: "module_reads",
                newName: "handout_reads");

            migrationBuilder.RenameTable(
                name: "module_items",
                newName: "handout_items");

            migrationBuilder.RenameTable(
                name: "material_modules",
                newName: "material_handouts");

            migrationBuilder.RenameColumn(
                name: "module_id",
                table: "material_items",
                newName: "handout_id");

            migrationBuilder.RenameIndex(
                name: "ix_material_items_module_id_position",
                table: "material_items",
                newName: "ix_material_items_handout_id_position");

            migrationBuilder.RenameColumn(
                name: "module_id",
                table: "material_deliveries",
                newName: "handout_id");

            migrationBuilder.RenameColumn(
                name: "material_module_id",
                table: "material_deliveries",
                newName: "material_handout_id");

            migrationBuilder.RenameIndex(
                name: "ix_material_deliveries_module_id",
                table: "material_deliveries",
                newName: "ix_material_deliveries_handout_id");

            migrationBuilder.RenameIndex(
                name: "ix_material_deliveries_material_module_id_assignment_id",
                table: "material_deliveries",
                newName: "ix_material_deliveries_material_handout_id_assignment_id");

            migrationBuilder.RenameColumn(
                name: "module_id",
                table: "attachments",
                newName: "handout_id");

            migrationBuilder.RenameColumn(
                name: "material_module_id",
                table: "attachments",
                newName: "material_handout_id");

            migrationBuilder.RenameIndex(
                name: "ix_attachments_module_id",
                table: "attachments",
                newName: "ix_attachments_handout_id");

            migrationBuilder.RenameIndex(
                name: "ix_attachments_material_module_id",
                table: "attachments",
                newName: "ix_attachments_material_handout_id");

            migrationBuilder.RenameIndex(
                name: "ix_modules_visible_from_utc",
                table: "handouts",
                newName: "ix_handouts_visible_from_utc");

            migrationBuilder.RenameIndex(
                name: "ix_modules_assignment_id_position",
                table: "handouts",
                newName: "ix_handouts_assignment_id_position");

            migrationBuilder.RenameColumn(
                name: "module_id",
                table: "handout_views",
                newName: "handout_id");

            migrationBuilder.RenameIndex(
                name: "ix_module_views_student_id",
                table: "handout_views",
                newName: "ix_handout_views_student_id");

            migrationBuilder.RenameColumn(
                name: "module_id",
                table: "handout_reads",
                newName: "handout_id");

            migrationBuilder.RenameIndex(
                name: "ix_module_reads_student_id",
                table: "handout_reads",
                newName: "ix_handout_reads_student_id");

            migrationBuilder.RenameColumn(
                name: "module_id",
                table: "handout_items",
                newName: "handout_id");

            migrationBuilder.RenameIndex(
                name: "ix_module_items_module_id_kind_position",
                table: "handout_items",
                newName: "ix_handout_items_handout_id_kind_position");

            migrationBuilder.RenameIndex(
                name: "ix_module_items_due_at_utc",
                table: "handout_items",
                newName: "ix_handout_items_due_at_utc");

            migrationBuilder.RenameIndex(
                name: "ix_material_modules_created_by_user_id",
                table: "material_handouts",
                newName: "ix_material_handouts_created_by_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_material_modules_course_id_position",
                table: "material_handouts",
                newName: "ix_material_handouts_course_id_position");

            migrationBuilder.AddPrimaryKey(
                name: "pk_handouts",
                table: "handouts",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_handout_views",
                table: "handout_views",
                columns: new[] { "handout_id", "student_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_handout_reads",
                table: "handout_reads",
                columns: new[] { "handout_id", "student_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_handout_items",
                table: "handout_items",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_material_handouts",
                table: "material_handouts",
                column: "id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_handout_reads_percent",
                table: "handout_reads",
                sql: "percent BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_handout_items_kind",
                table: "handout_items",
                sql: "kind IN ('Activity', 'Quiz', 'TaskPerformance', 'Seatwork', 'Recitation', 'Exam', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_handout_items_max_score",
                table: "handout_items",
                sql: "max_score > 0");

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_handout_items_item_id",
                table: "attachments",
                column: "item_id",
                principalTable: "handout_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_handouts_handout_id",
                table: "attachments",
                column: "handout_id",
                principalTable: "handouts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_material_handouts_material_handout_id",
                table: "attachments",
                column: "material_handout_id",
                principalTable: "material_handouts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_handout_items_handouts_handout_id",
                table: "handout_items",
                column: "handout_id",
                principalTable: "handouts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_handout_reads_handouts_handout_id",
                table: "handout_reads",
                column: "handout_id",
                principalTable: "handouts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_handout_reads_students_student_id",
                table: "handout_reads",
                column: "student_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_handout_views_handouts_handout_id",
                table: "handout_views",
                column: "handout_id",
                principalTable: "handouts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_handout_views_students_student_id",
                table: "handout_views",
                column: "student_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_handouts_teaching_assignments_assignment_id",
                table: "handouts",
                column: "assignment_id",
                principalTable: "teaching_assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_item_views_handout_items_item_id",
                table: "item_views",
                column: "item_id",
                principalTable: "handout_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_material_deliveries_handouts_handout_id",
                table: "material_deliveries",
                column: "handout_id",
                principalTable: "handouts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_material_deliveries_material_handouts_material_handout_id",
                table: "material_deliveries",
                column: "material_handout_id",
                principalTable: "material_handouts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_material_handouts_courses_course_id",
                table: "material_handouts",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_material_handouts_users_created_by_user_id",
                table: "material_handouts",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_material_items_material_handouts_handout_id",
                table: "material_items",
                column: "handout_id",
                principalTable: "material_handouts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_quiz_questions_handout_items_item_id",
                table: "quiz_questions",
                column: "item_id",
                principalTable: "handout_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_submissions_handout_items_item_id",
                table: "submissions",
                column: "item_id",
                principalTable: "handout_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attachments_handout_items_item_id",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "fk_attachments_handouts_handout_id",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "fk_attachments_material_handouts_material_handout_id",
                table: "attachments");

            migrationBuilder.DropForeignKey(
                name: "fk_handout_items_handouts_handout_id",
                table: "handout_items");

            migrationBuilder.DropForeignKey(
                name: "fk_handout_reads_handouts_handout_id",
                table: "handout_reads");

            migrationBuilder.DropForeignKey(
                name: "fk_handout_reads_students_student_id",
                table: "handout_reads");

            migrationBuilder.DropForeignKey(
                name: "fk_handout_views_handouts_handout_id",
                table: "handout_views");

            migrationBuilder.DropForeignKey(
                name: "fk_handout_views_students_student_id",
                table: "handout_views");

            migrationBuilder.DropForeignKey(
                name: "fk_handouts_teaching_assignments_assignment_id",
                table: "handouts");

            migrationBuilder.DropForeignKey(
                name: "fk_item_views_handout_items_item_id",
                table: "item_views");

            migrationBuilder.DropForeignKey(
                name: "fk_material_deliveries_handouts_handout_id",
                table: "material_deliveries");

            migrationBuilder.DropForeignKey(
                name: "fk_material_deliveries_material_handouts_material_handout_id",
                table: "material_deliveries");

            migrationBuilder.DropForeignKey(
                name: "fk_material_handouts_courses_course_id",
                table: "material_handouts");

            migrationBuilder.DropForeignKey(
                name: "fk_material_handouts_users_created_by_user_id",
                table: "material_handouts");

            migrationBuilder.DropForeignKey(
                name: "fk_material_items_material_handouts_handout_id",
                table: "material_items");

            migrationBuilder.DropForeignKey(
                name: "fk_quiz_questions_handout_items_item_id",
                table: "quiz_questions");

            migrationBuilder.DropForeignKey(
                name: "fk_submissions_handout_items_item_id",
                table: "submissions");

            migrationBuilder.DropPrimaryKey(
                name: "pk_material_handouts",
                table: "material_handouts");

            migrationBuilder.DropPrimaryKey(
                name: "pk_handouts",
                table: "handouts");

            migrationBuilder.DropPrimaryKey(
                name: "pk_handout_views",
                table: "handout_views");

            migrationBuilder.DropPrimaryKey(
                name: "pk_handout_reads",
                table: "handout_reads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_handout_reads_percent",
                table: "handout_reads");

            migrationBuilder.DropPrimaryKey(
                name: "pk_handout_items",
                table: "handout_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_handout_items_kind",
                table: "handout_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_handout_items_max_score",
                table: "handout_items");

            migrationBuilder.RenameTable(
                name: "material_handouts",
                newName: "material_modules");

            migrationBuilder.RenameTable(
                name: "handouts",
                newName: "modules");

            migrationBuilder.RenameTable(
                name: "handout_views",
                newName: "module_views");

            migrationBuilder.RenameTable(
                name: "handout_reads",
                newName: "module_reads");

            migrationBuilder.RenameTable(
                name: "handout_items",
                newName: "module_items");

            migrationBuilder.RenameColumn(
                name: "handout_id",
                table: "material_items",
                newName: "module_id");

            migrationBuilder.RenameIndex(
                name: "ix_material_items_handout_id_position",
                table: "material_items",
                newName: "ix_material_items_module_id_position");

            migrationBuilder.RenameColumn(
                name: "material_handout_id",
                table: "material_deliveries",
                newName: "material_module_id");

            migrationBuilder.RenameColumn(
                name: "handout_id",
                table: "material_deliveries",
                newName: "module_id");

            migrationBuilder.RenameIndex(
                name: "ix_material_deliveries_material_handout_id_assignment_id",
                table: "material_deliveries",
                newName: "ix_material_deliveries_material_module_id_assignment_id");

            migrationBuilder.RenameIndex(
                name: "ix_material_deliveries_handout_id",
                table: "material_deliveries",
                newName: "ix_material_deliveries_module_id");

            migrationBuilder.RenameColumn(
                name: "material_handout_id",
                table: "attachments",
                newName: "material_module_id");

            migrationBuilder.RenameColumn(
                name: "handout_id",
                table: "attachments",
                newName: "module_id");

            migrationBuilder.RenameIndex(
                name: "ix_attachments_material_handout_id",
                table: "attachments",
                newName: "ix_attachments_material_module_id");

            migrationBuilder.RenameIndex(
                name: "ix_attachments_handout_id",
                table: "attachments",
                newName: "ix_attachments_module_id");

            migrationBuilder.RenameIndex(
                name: "ix_material_handouts_created_by_user_id",
                table: "material_modules",
                newName: "ix_material_modules_created_by_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_material_handouts_course_id_position",
                table: "material_modules",
                newName: "ix_material_modules_course_id_position");

            migrationBuilder.RenameIndex(
                name: "ix_handouts_visible_from_utc",
                table: "modules",
                newName: "ix_modules_visible_from_utc");

            migrationBuilder.RenameIndex(
                name: "ix_handouts_assignment_id_position",
                table: "modules",
                newName: "ix_modules_assignment_id_position");

            migrationBuilder.RenameColumn(
                name: "handout_id",
                table: "module_views",
                newName: "module_id");

            migrationBuilder.RenameIndex(
                name: "ix_handout_views_student_id",
                table: "module_views",
                newName: "ix_module_views_student_id");

            migrationBuilder.RenameColumn(
                name: "handout_id",
                table: "module_reads",
                newName: "module_id");

            migrationBuilder.RenameIndex(
                name: "ix_handout_reads_student_id",
                table: "module_reads",
                newName: "ix_module_reads_student_id");

            migrationBuilder.RenameColumn(
                name: "handout_id",
                table: "module_items",
                newName: "module_id");

            migrationBuilder.RenameIndex(
                name: "ix_handout_items_handout_id_kind_position",
                table: "module_items",
                newName: "ix_module_items_module_id_kind_position");

            migrationBuilder.RenameIndex(
                name: "ix_handout_items_due_at_utc",
                table: "module_items",
                newName: "ix_module_items_due_at_utc");

            migrationBuilder.AddPrimaryKey(
                name: "pk_material_modules",
                table: "material_modules",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_modules",
                table: "modules",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_module_views",
                table: "module_views",
                columns: new[] { "module_id", "student_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_module_reads",
                table: "module_reads",
                columns: new[] { "module_id", "student_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_module_items",
                table: "module_items",
                column: "id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_module_reads_percent",
                table: "module_reads",
                sql: "percent BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_module_items_kind",
                table: "module_items",
                sql: "kind IN ('Activity', 'Quiz', 'TaskPerformance', 'Seatwork', 'Recitation', 'Exam', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_module_items_max_score",
                table: "module_items",
                sql: "max_score > 0");

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_material_modules_material_module_id",
                table: "attachments",
                column: "material_module_id",
                principalTable: "material_modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_module_items_item_id",
                table: "attachments",
                column: "item_id",
                principalTable: "module_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_modules_module_id",
                table: "attachments",
                column: "module_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_item_views_module_items_item_id",
                table: "item_views",
                column: "item_id",
                principalTable: "module_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_material_deliveries_material_modules_material_module_id",
                table: "material_deliveries",
                column: "material_module_id",
                principalTable: "material_modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_material_deliveries_modules_module_id",
                table: "material_deliveries",
                column: "module_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_material_items_material_modules_module_id",
                table: "material_items",
                column: "module_id",
                principalTable: "material_modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_material_modules_courses_course_id",
                table: "material_modules",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_material_modules_users_created_by_user_id",
                table: "material_modules",
                column: "created_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_module_items_modules_module_id",
                table: "module_items",
                column: "module_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_module_reads_modules_module_id",
                table: "module_reads",
                column: "module_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_module_reads_students_student_id",
                table: "module_reads",
                column: "student_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_module_views_modules_module_id",
                table: "module_views",
                column: "module_id",
                principalTable: "modules",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_module_views_students_student_id",
                table: "module_views",
                column: "student_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_modules_teaching_assignments_assignment_id",
                table: "modules",
                column: "assignment_id",
                principalTable: "teaching_assignments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_quiz_questions_module_items_item_id",
                table: "quiz_questions",
                column: "item_id",
                principalTable: "module_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_submissions_module_items_item_id",
                table: "submissions",
                column: "item_id",
                principalTable: "module_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
