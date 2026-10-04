using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <summary>
    /// Class offerings (a subject taught to a section in a term by a teacher) are not part of the system any more.
    /// Up drops the table and the rows in it; there was no screen that filled it, so it is normally empty.
    /// Down brings the empty table back.
    /// </summary>
    public partial class RemoveClassOfferings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "class_offerings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "class_offerings",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    section_id = table.Column<int>(type: "int", nullable: false),
                    subject_id = table.Column<int>(type: "int", nullable: false),
                    teacher_id = table.Column<int>(type: "int", nullable: true),
                    term_id = table.Column<int>(type: "int", nullable: false),
                    school_year_id = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_offerings", x => x.id);
                    table.ForeignKey(
                        name: "fk_class_offerings_sections_section_id_school_year_id",
                        columns: x => new { x.section_id, x.school_year_id },
                        principalTable: "sections",
                        principalColumns: new[] { "id", "school_year_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_class_offerings_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_class_offerings_teachers_teacher_id",
                        column: x => x.teacher_id,
                        principalTable: "teachers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_class_offerings_terms_term_id_school_year_id",
                        columns: x => new { x.term_id, x.school_year_id },
                        principalTable: "terms",
                        principalColumns: new[] { "id", "school_year_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_class_offerings_section_id_school_year_id",
                table: "class_offerings",
                columns: new[] { "section_id", "school_year_id" });

            migrationBuilder.CreateIndex(
                name: "ix_class_offerings_subject_id",
                table: "class_offerings",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_offerings_teacher_id",
                table: "class_offerings",
                column: "teacher_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_offerings_term_id_school_year_id",
                table: "class_offerings",
                columns: new[] { "term_id", "school_year_id" });

            migrationBuilder.CreateIndex(
                name: "ix_class_offerings_term_id_section_id_subject_id",
                table: "class_offerings",
                columns: new[] { "term_id", "section_id", "subject_id" },
                unique: true);
        }
    }
}
