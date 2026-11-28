using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdmissionRecordFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "sex",
                table: "students",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "sex",
                table: "admission_records",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldMaxLength: 10,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "address_city",
                table: "admission_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "address_postal_code",
                table: "admission_records",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "address_province",
                table: "admission_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "address_street",
                table: "admission_records",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "admission_records",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "father_email",
                table: "admission_records",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "father_occupation",
                table: "admission_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "guardian_city",
                table: "admission_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "guardian_email",
                table: "admission_records",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "guardian_occupation",
                table: "admission_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "guardian_postal_code",
                table: "admission_records",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "guardian_province",
                table: "admission_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "guardian_street",
                table: "admission_records",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "mother_email",
                table: "admission_records",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "mother_occupation",
                table: "admission_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "parents_address",
                table: "admission_records",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "parents_city",
                table: "admission_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "parents_postal_code",
                table: "admission_records",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "parents_province",
                table: "admission_records",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "parents_street",
                table: "admission_records",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "track_id",
                table: "admission_records",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "admission_options",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admission_options", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_admission_records_track_id",
                table: "admission_records",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ix_admission_options_kind_name",
                table: "admission_options",
                columns: new[] { "kind", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_admission_records_tracks_track_id",
                table: "admission_records",
                column: "track_id",
                principalTable: "tracks",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Round 49: the lists of the record form start with the schools and the places of Albay (the Admission staff add the ones that are missing with Add school and Add address).
            migrationBuilder.Sql(
                "INSERT IGNORE INTO `admission_options` (`kind`, `name`, `created_at_utc`, `updated_at_utc`) VALUES " +
                "('School', 'Albay National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Legazpi City National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Daraga National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Ligao National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Tabaco National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Camalig National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Guinobatan National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Polangui National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Oas National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Libon National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Pio Duran National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Santo Domingo National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Malinao National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Manito National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Tiwi National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Bacacay National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Jovellar National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Rapu-Rapu National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Malilipot National High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('School', 'Bicol University Laboratory High School', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));");
            migrationBuilder.Sql(
                "INSERT IGNORE INTO `admission_options` (`kind`, `name`, `created_at_utc`, `updated_at_utc`) VALUES " +
                "('City', 'Legazpi City', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Ligao City', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Tabaco City', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Bacacay', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Camalig', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Daraga', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Guinobatan', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Jovellar', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Libon', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Malilipot', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Malinao', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Manito', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Oas', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Pio Duran', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Polangui', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Rapu-Rapu', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Santo Domingo', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)), ('City', 'Tiwi', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_admission_records_tracks_track_id",
                table: "admission_records");

            migrationBuilder.DropTable(
                name: "admission_options");

            migrationBuilder.DropIndex(
                name: "ix_admission_records_track_id",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "address_city",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "address_postal_code",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "address_province",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "address_street",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "email",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "father_email",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "father_occupation",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "guardian_city",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "guardian_email",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "guardian_occupation",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "guardian_postal_code",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "guardian_province",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "guardian_street",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "mother_email",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "mother_occupation",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "parents_address",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "parents_city",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "parents_postal_code",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "parents_province",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "parents_street",
                table: "admission_records");

            migrationBuilder.DropColumn(
                name: "track_id",
                table: "admission_records");

            migrationBuilder.AlterColumn<string>(
                name: "sex",
                table: "students",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "sex",
                table: "admission_records",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
