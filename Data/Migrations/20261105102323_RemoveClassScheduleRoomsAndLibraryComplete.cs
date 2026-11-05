using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClassScheduleRoomsAndLibraryComplete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "course_schedules");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropTable(
                name: "schedules");

            migrationBuilder.DropColumn(
                name: "library_completed_at_utc",
                table: "subjects");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "library_completed_at_utc",
                table: "subjects",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "schedules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    start_time = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedules", x => x.id);
                    table.CheckConstraint("ck_schedules_hours", "end_time > start_time");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "course_schedules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    room_id = table.Column<int>(type: "int", nullable: true),
                    schedule_id = table.Column<int>(type: "int", nullable: false),
                    section_id = table.Column<int>(type: "int", nullable: false),
                    subject_id = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    days = table.Column<string>(type: "varchar(6)", maxLength: 6, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    end_time = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_schedules", x => x.id);
                    table.CheckConstraint("ck_course_schedules_days", "days REGEXP '^M?T?W?(TH)?F?$' AND days <> ''");
                    table.CheckConstraint("ck_course_schedules_hours", "end_time > start_time");
                    table.ForeignKey(
                        name: "fk_course_schedules_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_course_schedules_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalTable: "schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_schedules_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_course_schedules_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_course_schedules_room_id",
                table: "course_schedules",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_schedules_schedule_id",
                table: "course_schedules",
                column: "schedule_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_schedules_section_id_subject_id_days",
                table: "course_schedules",
                columns: new[] { "section_id", "subject_id", "days" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_schedules_subject_id",
                table: "course_schedules",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_name",
                table: "rooms",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedules_code",
                table: "schedules",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedules_name",
                table: "schedules",
                column: "name",
                unique: true);
        }
    }
}
