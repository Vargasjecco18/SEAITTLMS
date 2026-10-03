using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <summary>
    /// Grade level, track, strand, subject and section on students and teachers (all optional).
    ///
    /// They are foreign keys, and the constraints keep them from contradicting each other: a section, strand and grade
    /// level given together must be the section's own, a strand and track given together must belong together, a
    /// strand needs its track, a section needs its strand and grade level (composite foreign keys + CHECKs).
    ///
    /// The five columns are added with raw SQL (AFTER ...) only to control their order in the table.
    /// </summary>
    public partial class AddPlacementToStudentsAndTeachers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Same five columns on both tables, in the order a person reads them, right after the person's details.
            migrationBuilder.Sql(AddPlacementColumns("teachers", after: "name_extension"));
            migrationBuilder.Sql(AddPlacementColumns("students", after: "sex"));

            migrationBuilder.AddUniqueConstraint(
                name: "ak_strands_id_track_id",
                table: "strands",
                columns: new[] { "id", "track_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_sections_id_strand_id_grade_level",
                table: "sections",
                columns: new[] { "id", "strand_id", "grade_level" });

            migrationBuilder.CreateIndex(
                name: "ix_teachers_section_id_strand_id_grade_level",
                table: "teachers",
                columns: new[] { "section_id", "strand_id", "grade_level" });

            migrationBuilder.CreateIndex(
                name: "ix_teachers_strand_id_track_id",
                table: "teachers",
                columns: new[] { "strand_id", "track_id" });

            migrationBuilder.CreateIndex(
                name: "ix_teachers_subject_id",
                table: "teachers",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_teachers_track_id",
                table: "teachers",
                column: "track_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_teachers_grade_level",
                table: "teachers",
                sql: "grade_level IS NULL OR grade_level IN (11, 12)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_teachers_section_needs_strand_and_grade",
                table: "teachers",
                sql: "section_id IS NULL OR (strand_id IS NOT NULL AND grade_level IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_teachers_strand_needs_track",
                table: "teachers",
                sql: "strand_id IS NULL OR track_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_students_section_id_strand_id_grade_level",
                table: "students",
                columns: new[] { "section_id", "strand_id", "grade_level" });

            migrationBuilder.CreateIndex(
                name: "ix_students_strand_id_track_id",
                table: "students",
                columns: new[] { "strand_id", "track_id" });

            migrationBuilder.CreateIndex(
                name: "ix_students_subject_id",
                table: "students",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_track_id",
                table: "students",
                column: "track_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_grade_level",
                table: "students",
                sql: "grade_level IS NULL OR grade_level IN (11, 12)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_section_needs_strand_and_grade",
                table: "students",
                sql: "section_id IS NULL OR (strand_id IS NOT NULL AND grade_level IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_students_strand_needs_track",
                table: "students",
                sql: "strand_id IS NULL OR track_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_students_sections_section_id_strand_id_grade_level",
                table: "students",
                columns: new[] { "section_id", "strand_id", "grade_level" },
                principalTable: "sections",
                principalColumns: new[] { "id", "strand_id", "grade_level" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_students_strands_strand_id_track_id",
                table: "students",
                columns: new[] { "strand_id", "track_id" },
                principalTable: "strands",
                principalColumns: new[] { "id", "track_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_students_subjects_subject_id",
                table: "students",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_students_tracks_track_id",
                table: "students",
                column: "track_id",
                principalTable: "tracks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_teachers_sections_section_id_strand_id_grade_level",
                table: "teachers",
                columns: new[] { "section_id", "strand_id", "grade_level" },
                principalTable: "sections",
                principalColumns: new[] { "id", "strand_id", "grade_level" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_teachers_strands_strand_id_track_id",
                table: "teachers",
                columns: new[] { "strand_id", "track_id" },
                principalTable: "strands",
                principalColumns: new[] { "id", "track_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_teachers_subjects_subject_id",
                table: "teachers",
                column: "subject_id",
                principalTable: "subjects",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_teachers_tracks_track_id",
                table: "teachers",
                column: "track_id",
                principalTable: "tracks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_students_sections_section_id_strand_id_grade_level",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "fk_students_strands_strand_id_track_id",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "fk_students_subjects_subject_id",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "fk_students_tracks_track_id",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "fk_teachers_sections_section_id_strand_id_grade_level",
                table: "teachers");

            migrationBuilder.DropForeignKey(
                name: "fk_teachers_strands_strand_id_track_id",
                table: "teachers");

            migrationBuilder.DropForeignKey(
                name: "fk_teachers_subjects_subject_id",
                table: "teachers");

            migrationBuilder.DropForeignKey(
                name: "fk_teachers_tracks_track_id",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "ix_teachers_section_id_strand_id_grade_level",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "ix_teachers_strand_id_track_id",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "ix_teachers_subject_id",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "ix_teachers_track_id",
                table: "teachers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_teachers_grade_level",
                table: "teachers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_teachers_section_needs_strand_and_grade",
                table: "teachers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_teachers_strand_needs_track",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "ix_students_section_id_strand_id_grade_level",
                table: "students");

            migrationBuilder.DropIndex(
                name: "ix_students_strand_id_track_id",
                table: "students");

            migrationBuilder.DropIndex(
                name: "ix_students_subject_id",
                table: "students");

            migrationBuilder.DropIndex(
                name: "ix_students_track_id",
                table: "students");

            migrationBuilder.DropCheckConstraint(
                name: "ck_students_grade_level",
                table: "students");

            migrationBuilder.DropCheckConstraint(
                name: "ck_students_section_needs_strand_and_grade",
                table: "students");

            migrationBuilder.DropCheckConstraint(
                name: "ck_students_strand_needs_track",
                table: "students");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_strands_id_track_id",
                table: "strands");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_sections_id_strand_id_grade_level",
                table: "sections");

            migrationBuilder.DropColumn(
                name: "grade_level",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "section_id",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "strand_id",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "subject_id",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "track_id",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "grade_level",
                table: "students");

            migrationBuilder.DropColumn(
                name: "section_id",
                table: "students");

            migrationBuilder.DropColumn(
                name: "strand_id",
                table: "students");

            migrationBuilder.DropColumn(
                name: "subject_id",
                table: "students");

            migrationBuilder.DropColumn(
                name: "track_id",
                table: "students");
        }
        private static string AddPlacementColumns(string table, string after) => $"""
            ALTER TABLE `{table}`
                ADD COLUMN `grade_level` tinyint unsigned NULL AFTER `{after}`,
                ADD COLUMN `track_id` int NULL AFTER `grade_level`,
                ADD COLUMN `strand_id` int NULL AFTER `track_id`,
                ADD COLUMN `subject_id` int NULL AFTER `strand_id`,
                ADD COLUMN `section_id` int NULL AFTER `subject_id`;
            """;
    }
}
