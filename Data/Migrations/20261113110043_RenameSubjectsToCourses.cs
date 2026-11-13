using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameSubjectsToCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_enrollment_subjects_enrollments_enrollment_id",
                table: "enrollment_subjects");

            migrationBuilder.DropForeignKey(
                name: "fk_enrollment_subjects_subjects_subject_id",
                table: "enrollment_subjects");

            migrationBuilder.DropForeignKey(
                name: "fk_material_modules_subjects_subject_id",
                table: "material_modules");

            migrationBuilder.DropForeignKey(
                name: "fk_strand_subjects_strands_strand_id",
                table: "strand_subjects");

            migrationBuilder.DropForeignKey(
                name: "fk_strand_subjects_subjects_subject_id",
                table: "strand_subjects");

            migrationBuilder.DropForeignKey(
                name: "fk_students_subjects_subject_id",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "fk_teachers_subjects_subject_id",
                table: "teachers");

            migrationBuilder.DropForeignKey(
                name: "fk_teaching_assignments_subjects_subject_id",
                table: "teaching_assignments");

            migrationBuilder.DropPrimaryKey(
                name: "pk_subjects",
                table: "subjects");

            migrationBuilder.DropCheckConstraint(
                name: "ck_subjects_units",
                table: "subjects");

            migrationBuilder.DropPrimaryKey(
                name: "pk_strand_subjects",
                table: "strand_subjects");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strand_subjects_grade_level",
                table: "strand_subjects");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strand_subjects_term_number",
                table: "strand_subjects");

            migrationBuilder.DropPrimaryKey(
                name: "pk_enrollment_subjects",
                table: "enrollment_subjects");

            migrationBuilder.RenameTable(
                name: "subjects",
                newName: "courses");

            migrationBuilder.RenameTable(
                name: "strand_subjects",
                newName: "strand_courses");

            migrationBuilder.RenameTable(
                name: "enrollment_subjects",
                newName: "enrollment_courses");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                table: "teaching_assignments",
                newName: "course_id");

            migrationBuilder.RenameIndex(
                name: "ix_teaching_assignments_subject_id",
                table: "teaching_assignments",
                newName: "ix_teaching_assignments_course_id");

            migrationBuilder.RenameIndex(
                name: "ix_teaching_assignments_section_id_subject_id",
                table: "teaching_assignments",
                newName: "ix_teaching_assignments_section_id_course_id");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                table: "teachers",
                newName: "course_id");

            migrationBuilder.RenameIndex(
                name: "ix_teachers_subject_id",
                table: "teachers",
                newName: "ix_teachers_course_id");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                table: "students",
                newName: "course_id");

            migrationBuilder.RenameIndex(
                name: "ix_students_subject_id",
                table: "students",
                newName: "ix_students_course_id");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                table: "material_modules",
                newName: "course_id");

            migrationBuilder.RenameIndex(
                name: "ix_material_modules_subject_id_position",
                table: "material_modules",
                newName: "ix_material_modules_course_id_position");

            migrationBuilder.RenameColumn(
                name: "subjects",
                table: "enrollments",
                newName: "courses");

            migrationBuilder.RenameIndex(
                name: "ix_subjects_code",
                table: "courses",
                newName: "ix_courses_code");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                table: "strand_courses",
                newName: "course_id");

            migrationBuilder.RenameIndex(
                name: "ix_strand_subjects_subject_id",
                table: "strand_courses",
                newName: "ix_strand_courses_course_id");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                table: "enrollment_courses",
                newName: "course_id");

            migrationBuilder.RenameIndex(
                name: "ix_enrollment_subjects_subject_id",
                table: "enrollment_courses",
                newName: "ix_enrollment_courses_course_id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_courses",
                table: "courses",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_strand_courses",
                table: "strand_courses",
                columns: new[] { "strand_id", "grade_level", "term_number", "course_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_enrollment_courses",
                table: "enrollment_courses",
                columns: new[] { "enrollment_id", "course_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_units",
                table: "courses",
                sql: "units >= 0.5 AND units <= 12");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strand_courses_grade_level",
                table: "strand_courses",
                sql: "grade_level IN (11, 12)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strand_courses_term_number",
                table: "strand_courses",
                sql: "term_number >= 1");

            migrationBuilder.AddForeignKey(
                name: "fk_enrollment_courses_courses_course_id",
                table: "enrollment_courses",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_enrollment_courses_enrollments_enrollment_id",
                table: "enrollment_courses",
                column: "enrollment_id",
                principalTable: "enrollments",
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
                name: "fk_strand_courses_courses_course_id",
                table: "strand_courses",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_strand_courses_strands_strand_id",
                table: "strand_courses",
                column: "strand_id",
                principalTable: "strands",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_students_courses_course_id",
                table: "students",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_teachers_courses_course_id",
                table: "teachers",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_teaching_assignments_courses_course_id",
                table: "teaching_assignments",
                column: "course_id",
                principalTable: "courses",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_enrollment_courses_courses_course_id",
                table: "enrollment_courses");

            migrationBuilder.DropForeignKey(
                name: "fk_enrollment_courses_enrollments_enrollment_id",
                table: "enrollment_courses");

            migrationBuilder.DropForeignKey(
                name: "fk_material_modules_courses_course_id",
                table: "material_modules");

            migrationBuilder.DropForeignKey(
                name: "fk_strand_courses_courses_course_id",
                table: "strand_courses");

            migrationBuilder.DropForeignKey(
                name: "fk_strand_courses_strands_strand_id",
                table: "strand_courses");

            migrationBuilder.DropForeignKey(
                name: "fk_students_courses_course_id",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "fk_teachers_courses_course_id",
                table: "teachers");

            migrationBuilder.DropForeignKey(
                name: "fk_teaching_assignments_courses_course_id",
                table: "teaching_assignments");

            migrationBuilder.DropPrimaryKey(
                name: "pk_strand_courses",
                table: "strand_courses");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strand_courses_grade_level",
                table: "strand_courses");

            migrationBuilder.DropCheckConstraint(
                name: "ck_strand_courses_term_number",
                table: "strand_courses");

            migrationBuilder.DropPrimaryKey(
                name: "pk_enrollment_courses",
                table: "enrollment_courses");

            migrationBuilder.DropPrimaryKey(
                name: "pk_courses",
                table: "courses");

            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_units",
                table: "courses");

            migrationBuilder.RenameTable(
                name: "strand_courses",
                newName: "strand_subjects");

            migrationBuilder.RenameTable(
                name: "enrollment_courses",
                newName: "enrollment_subjects");

            migrationBuilder.RenameTable(
                name: "courses",
                newName: "subjects");

            migrationBuilder.RenameColumn(
                name: "course_id",
                table: "teaching_assignments",
                newName: "subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_teaching_assignments_section_id_course_id",
                table: "teaching_assignments",
                newName: "ix_teaching_assignments_section_id_subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_teaching_assignments_course_id",
                table: "teaching_assignments",
                newName: "ix_teaching_assignments_subject_id");

            migrationBuilder.RenameColumn(
                name: "course_id",
                table: "teachers",
                newName: "subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_teachers_course_id",
                table: "teachers",
                newName: "ix_teachers_subject_id");

            migrationBuilder.RenameColumn(
                name: "course_id",
                table: "students",
                newName: "subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_students_course_id",
                table: "students",
                newName: "ix_students_subject_id");

            migrationBuilder.RenameColumn(
                name: "course_id",
                table: "material_modules",
                newName: "subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_material_modules_course_id_position",
                table: "material_modules",
                newName: "ix_material_modules_subject_id_position");

            migrationBuilder.RenameColumn(
                name: "courses",
                table: "enrollments",
                newName: "subjects");

            migrationBuilder.RenameColumn(
                name: "course_id",
                table: "strand_subjects",
                newName: "subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_strand_courses_course_id",
                table: "strand_subjects",
                newName: "ix_strand_subjects_subject_id");

            migrationBuilder.RenameColumn(
                name: "course_id",
                table: "enrollment_subjects",
                newName: "subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_enrollment_courses_course_id",
                table: "enrollment_subjects",
                newName: "ix_enrollment_subjects_subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_courses_code",
                table: "subjects",
                newName: "ix_subjects_code");

            migrationBuilder.AddPrimaryKey(
                name: "pk_strand_subjects",
                table: "strand_subjects",
                columns: new[] { "strand_id", "grade_level", "term_number", "subject_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_enrollment_subjects",
                table: "enrollment_subjects",
                columns: new[] { "enrollment_id", "subject_id" });

            migrationBuilder.AddPrimaryKey(
                name: "pk_subjects",
                table: "subjects",
                column: "id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strand_subjects_grade_level",
                table: "strand_subjects",
                sql: "grade_level IN (11, 12)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_strand_subjects_term_number",
                table: "strand_subjects",
                sql: "term_number >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_subjects_units",
                table: "subjects",
                sql: "units >= 0.5 AND units <= 12");

            migrationBuilder.AddForeignKey(
                name: "fk_enrollment_subjects_enrollments_enrollment_id",
                table: "enrollment_subjects",
                column: "enrollment_id",
                principalTable: "enrollments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_enrollment_subjects_subjects_subject_id",
                table: "enrollment_subjects",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_material_modules_subjects_subject_id",
                table: "material_modules",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_strand_subjects_strands_strand_id",
                table: "strand_subjects",
                column: "strand_id",
                principalTable: "strands",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_strand_subjects_subjects_subject_id",
                table: "strand_subjects",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_students_subjects_subject_id",
                table: "students",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_teachers_subjects_subject_id",
                table: "teachers",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_teaching_assignments_subjects_subject_id",
                table: "teaching_assignments",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
