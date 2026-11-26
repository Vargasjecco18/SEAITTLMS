using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdmissionStrandChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "strand_id",
                table: "admission_records",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_admission_records_strand_id",
                table: "admission_records",
                column: "strand_id");

            migrationBuilder.AddForeignKey(
                name: "fk_admission_records_strands_strand_id",
                table: "admission_records",
                column: "strand_id",
                principalTable: "strands",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_admission_records_strands_strand_id",
                table: "admission_records");

            migrationBuilder.DropIndex(
                name: "ix_admission_records_strand_id",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "strand_id",
                table: "admission_records");
        }
    }
}
