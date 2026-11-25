using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdmissionRecordsAndStrandPlacement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_students_courses_course_id",
                table: "students");

            migrationBuilder.DropForeignKey(
                name: "fk_teachers_courses_course_id",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "ix_teachers_course_id",
                table: "teachers");

            migrationBuilder.DropIndex(
                name: "ix_students_course_id",
                table: "students");

            migrationBuilder.DropColumn(
                name: "course_id",
                table: "teachers");

            migrationBuilder.DropColumn(
                name: "birth_date",
                table: "students");

            migrationBuilder.DropColumn(
                name: "course_id",
                table: "students");

            migrationBuilder.CreateTable(
                name: "admission_records",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    grade_level = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    lrn = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    first_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    middle_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name_extension = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sex = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone_number = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    address = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    previous_school = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    father_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    father_phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mother_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    mother_phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    guardian_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    guardian_relationship = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    guardian_phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    guardian_address = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recorded_by_user_id = table.Column<int>(type: "int", nullable: true),
                    enrolled_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admission_records", x => x.id);
                    table.CheckConstraint("ck_admission_records_grade_level", "grade_level IN (11, 12)");
                    table.CheckConstraint("ck_admission_records_lrn", "CHAR_LENGTH(lrn) = 12");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "admission_documents",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    record_id = table.Column<int>(type: "int", nullable: false),
                    kind = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    data = table.Column<byte[]>(type: "longblob", nullable: true),
                    received_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    received_by_user_id = table.Column<int>(type: "int", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admission_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_admission_documents_admission_records_record_id",
                        column: x => x.record_id,
                        principalTable: "admission_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_admission_documents_record_id_kind",
                table: "admission_documents",
                columns: new[] { "record_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_admission_records_grade_level_last_name",
                table: "admission_records",
                columns: new[] { "grade_level", "last_name" });

            migrationBuilder.CreateIndex(
                name: "ix_admission_records_lrn",
                table: "admission_records",
                column: "lrn",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admission_documents");

            migrationBuilder.DropTable(
                name: "admission_records");

            migrationBuilder.AddColumn<int>(
                name: "course_id",
                table: "teachers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "birth_date",
                table: "students",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "course_id",
                table: "students",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_teachers_course_id",
                table: "teachers",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_course_id",
                table: "students",
                column: "course_id");

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
        }
    }
}
