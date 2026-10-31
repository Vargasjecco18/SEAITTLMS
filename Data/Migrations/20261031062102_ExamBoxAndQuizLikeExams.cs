using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExamBoxAndQuizLikeExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_exam_set",
                table: "modules",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // The "Summative exams" modules the provider's exams were delivered into before are exam boxes now: they are not listed as modules.
            migrationBuilder.Sql(
                "UPDATE modules m JOIN material_deliveries d ON d.module_id = m.id JOIN material_modules mm ON mm.id = d.material_module_id " +
                "SET m.is_exam_set = 1 WHERE mm.is_exam_set = 1 AND m.id > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_exam_set",
                table: "modules");
        }
    }
}
