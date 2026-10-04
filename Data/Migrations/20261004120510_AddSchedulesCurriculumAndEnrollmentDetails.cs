using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SEAITT_LMS.Data.Migrations
{
    /// <summary>
    /// What the registrar needs: the schedules (AM, MID, PM), the curriculum (which subjects each strand takes in each
    /// term), and a fuller enrollment record. Also puts this school's data in the database: school year 2026-2027 with
    /// its two terms, the tracks ACAD and TECH-PRO, eight strands, their schedules and their subjects.
    ///
    /// - schedules, strand_schedules: AM, MID and PM, and which of them each strand runs in. A section has a schedule,
    ///   and the database makes sure it is one of its strand's.
    /// - strand_subjects: the curriculum. The registrar picks a student's subjects from it.
    /// - enrollments: now also hold the student's LRN, name, grade level, track, strand and subjects as they were when
    ///   the student was enrolled (rows that already exist are filled in from the student and the section).
    ///   enrollment_subjects has one row per subject of an enrollment.
    /// - tracks: TVL becomes TECH-PRO, SPORTS and ARTS are removed (or switched off when something uses them).
    ///
    /// The seed data is written so that running it twice, or on a database where some of the rows already exist, adds
    /// only what is missing. The columns of enrollments and sections are added with raw SQL (AFTER ...) to control their
    /// order in the table.
    /// </summary>
    public partial class AddSchedulesCurriculumAndEnrollmentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- Tracks: this school has ACAD and TECH-PRO. Done before enrollments get a track column.
            migrationBuilder.Sql(
                "UPDATE `tracks` SET `code` = 'TECH-PRO', `name` = 'Technical-Professional' " +
                "WHERE `id` = 2 AND `code` = 'TVL' " +
                "AND NOT EXISTS (SELECT 1 FROM (SELECT `id` FROM `tracks` WHERE `code` = 'TECH-PRO' OR `name` = 'Technical-Professional') AS `taken`);");
            migrationBuilder.Sql(
                "DELETE FROM `tracks` WHERE `id` IN (3, 4) AND `code` IN ('SPORTS', 'ARTS') " +
                "AND NOT EXISTS (SELECT 1 FROM `strands` WHERE `strands`.`track_id` = `tracks`.`id`) " +
                "AND NOT EXISTS (SELECT 1 FROM `students` WHERE `students`.`track_id` = `tracks`.`id`) " +
                "AND NOT EXISTS (SELECT 1 FROM `teachers` WHERE `teachers`.`track_id` = `tracks`.`id`);");
            // A track that is still in use is kept, but switched off.
            migrationBuilder.Sql("UPDATE `tracks` SET `is_active` = 0 WHERE `id` IN (3, 4) AND `code` IN ('SPORTS', 'ARTS');");

            // ---- New columns, nullable first. They sit next to the columns they belong with.
            migrationBuilder.Sql("ALTER TABLE `sections` ADD COLUMN `schedule_id` int NULL AFTER `grade_level`;");
            migrationBuilder.Sql(
                "ALTER TABLE `enrollments` " +
                "ADD COLUMN `lrn` char(12) NULL AFTER `student_id`, " +
                "ADD COLUMN `first_name` varchar(100) NULL AFTER `lrn`, " +
                "ADD COLUMN `middle_name` varchar(100) NULL AFTER `first_name`, " +
                "ADD COLUMN `last_name` varchar(100) NULL AFTER `middle_name`, " +
                "ADD COLUMN `grade_level` tinyint unsigned NULL AFTER `last_name`, " +
                "ADD COLUMN `track_id` int NULL AFTER `grade_level`, " +
                "ADD COLUMN `strand_id` int NULL AFTER `track_id`, " +
                "ADD COLUMN `subjects` varchar(1000) NULL AFTER `strand_id`;");

            migrationBuilder.CreateTable(
                name: "enrollment_subjects",
                columns: table => new
                {
                    enrollment_id = table.Column<int>(type: "int", nullable: false),
                    subject_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrollment_subjects", x => new { x.enrollment_id, x.subject_id });
                    table.ForeignKey(
                        name: "fk_enrollment_subjects_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_enrollment_subjects_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                    name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedules", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "strand_subjects",
                columns: table => new
                {
                    strand_id = table.Column<int>(type: "int", nullable: false),
                    grade_level = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    term_number = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    subject_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_strand_subjects", x => new { x.strand_id, x.grade_level, x.term_number, x.subject_id });
                    table.CheckConstraint("ck_strand_subjects_grade_level", "grade_level IN (11, 12)");
                    table.CheckConstraint("ck_strand_subjects_term_number", "term_number >= 1");
                    table.ForeignKey(
                        name: "fk_strand_subjects_strands_strand_id",
                        column: x => x.strand_id,
                        principalTable: "strands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_strand_subjects_subjects_subject_id",
                        column: x => x.subject_id,
                        principalTable: "subjects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "strand_schedules",
                columns: table => new
                {
                    strand_id = table.Column<int>(type: "int", nullable: false),
                    schedule_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_strand_schedules", x => new { x.strand_id, x.schedule_id });
                    table.ForeignKey(
                        name: "fk_strand_schedules_schedules_schedule_id",
                        column: x => x.schedule_id,
                        principalTable: "schedules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_strand_schedules_strands_strand_id",
                        column: x => x.strand_id,
                        principalTable: "strands",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // ---- This school's data. Each statement adds only the rows that are not there yet.
            SeedSchoolData(migrationBuilder);

            // ---- Fill in the enrollments that already exist from the student and the section, then make the columns required.
            migrationBuilder.Sql(
                "UPDATE `enrollments` e " +
                "JOIN `students` s ON s.`id` = e.`student_id` " +
                "JOIN `sections` sec ON sec.`id` = e.`section_id` " +
                "JOIN `strands` st ON st.`id` = sec.`strand_id` " +
                "SET e.`lrn` = s.`lrn`, e.`first_name` = s.`first_name`, e.`middle_name` = s.`middle_name`, " +
                "e.`last_name` = s.`last_name`, e.`grade_level` = sec.`grade_level`, " +
                "e.`strand_id` = sec.`strand_id`, e.`track_id` = st.`track_id` " +
                "WHERE e.`id` > 0;");
            migrationBuilder.Sql(
                "ALTER TABLE `enrollments` " +
                "MODIFY COLUMN `lrn` char(12) NOT NULL, " +
                "MODIFY COLUMN `first_name` varchar(100) NOT NULL, " +
                "MODIFY COLUMN `last_name` varchar(100) NOT NULL, " +
                "MODIFY COLUMN `grade_level` tinyint unsigned NOT NULL, " +
                "MODIFY COLUMN `track_id` int NOT NULL, " +
                "MODIFY COLUMN `strand_id` int NOT NULL;");

            // Until now a student had every subject of the section's classes. Keep that for the existing enrollments.
            migrationBuilder.Sql(
                "INSERT INTO `enrollment_subjects` (`enrollment_id`, `subject_id`) " +
                "SELECT DISTINCT e.`id`, co.`subject_id` FROM `enrollments` e " +
                "JOIN `class_offerings` co ON co.`term_id` = e.`term_id` AND co.`section_id` = e.`section_id` " +
                "WHERE e.`status` = 'Enrolled';");
            migrationBuilder.Sql(
                "UPDATE `enrollments` e " +
                "SET e.`subjects` = (SELECT LEFT(GROUP_CONCAT(sub.`name` ORDER BY sub.`name` SEPARATOR ', '), 1000) " +
                "FROM `enrollment_subjects` es JOIN `subjects` sub ON sub.`id` = es.`subject_id` WHERE es.`enrollment_id` = e.`id`) " +
                "WHERE e.`id` > 0;");

            // The new indexes first and the old ones after: MySQL refuses to drop an index that a foreign key still needs.
            migrationBuilder.CreateIndex(
                name: "ix_sections_schedule_id",
                table: "sections",
                column: "schedule_id");

            migrationBuilder.CreateIndex(
                name: "ix_sections_school_year_id_strand_id_grade_level_schedule_id_na",
                table: "sections",
                columns: new[] { "school_year_id", "strand_id", "grade_level", "schedule_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sections_strand_id_schedule_id",
                table: "sections",
                columns: new[] { "strand_id", "schedule_id" });

            migrationBuilder.DropIndex(
                name: "ix_sections_school_year_id_strand_id_grade_level_name",
                table: "sections");

            migrationBuilder.DropIndex(
                name: "ix_sections_strand_id",
                table: "sections");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_section_id_strand_id_grade_level",
                table: "enrollments",
                columns: new[] { "section_id", "strand_id", "grade_level" });

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_strand_id_track_id",
                table: "enrollments",
                columns: new[] { "strand_id", "track_id" });

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_track_id",
                table: "enrollments",
                column: "track_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_enrollments_grade_level",
                table: "enrollments",
                sql: "grade_level IN (11, 12)");

            migrationBuilder.CreateIndex(
                name: "ix_enrollment_subjects_subject_id",
                table: "enrollment_subjects",
                column: "subject_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_strand_schedules_schedule_id",
                table: "strand_schedules",
                column: "schedule_id");

            migrationBuilder.CreateIndex(
                name: "ix_strand_subjects_subject_id",
                table: "strand_subjects",
                column: "subject_id");

            migrationBuilder.AddForeignKey(
                name: "fk_enrollments_sections_section_id_strand_id_grade_level",
                table: "enrollments",
                columns: new[] { "section_id", "strand_id", "grade_level" },
                principalTable: "sections",
                principalColumns: new[] { "id", "strand_id", "grade_level" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_enrollments_strands_strand_id_track_id",
                table: "enrollments",
                columns: new[] { "strand_id", "track_id" },
                principalTable: "strands",
                principalColumns: new[] { "id", "track_id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_enrollments_tracks_track_id",
                table: "enrollments",
                column: "track_id",
                principalTable: "tracks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sections_schedules_schedule_id",
                table: "sections",
                column: "schedule_id",
                principalTable: "schedules",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_sections_strand_schedules_strand_id_schedule_id",
                table: "sections",
                columns: new[] { "strand_id", "schedule_id" },
                principalTable: "strand_schedules",
                principalColumns: new[] { "strand_id", "schedule_id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_enrollments_sections_section_id_strand_id_grade_level",
                table: "enrollments");

            migrationBuilder.DropForeignKey(
                name: "fk_enrollments_strands_strand_id_track_id",
                table: "enrollments");

            migrationBuilder.DropForeignKey(
                name: "fk_enrollments_tracks_track_id",
                table: "enrollments");

            migrationBuilder.DropForeignKey(
                name: "fk_sections_schedules_schedule_id",
                table: "sections");

            migrationBuilder.DropForeignKey(
                name: "fk_sections_strand_schedules_strand_id_schedule_id",
                table: "sections");

            migrationBuilder.DropTable(
                name: "enrollment_subjects");

            migrationBuilder.DropTable(
                name: "strand_schedules");

            migrationBuilder.DropTable(
                name: "strand_subjects");

            // The old indexes first again, then the new ones can go (the foreign keys need one of them at all times).
            migrationBuilder.CreateIndex(
                name: "ix_sections_school_year_id_strand_id_grade_level_name",
                table: "sections",
                columns: new[] { "school_year_id", "strand_id", "grade_level", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sections_strand_id",
                table: "sections",
                column: "strand_id");

            migrationBuilder.DropIndex(
                name: "ix_sections_schedule_id",
                table: "sections");

            migrationBuilder.DropIndex(
                name: "ix_sections_school_year_id_strand_id_grade_level_schedule_id_na",
                table: "sections");

            migrationBuilder.DropIndex(
                name: "ix_sections_strand_id_schedule_id",
                table: "sections");

            migrationBuilder.DropIndex(
                name: "ix_enrollments_section_id_strand_id_grade_level",
                table: "enrollments");

            migrationBuilder.DropIndex(
                name: "ix_enrollments_strand_id_track_id",
                table: "enrollments");

            migrationBuilder.DropIndex(
                name: "ix_enrollments_track_id",
                table: "enrollments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_enrollments_grade_level",
                table: "enrollments");

            migrationBuilder.DropTable(
                name: "schedules");

            migrationBuilder.DropColumn(
                name: "schedule_id",
                table: "sections");

            migrationBuilder.DropColumn(
                name: "first_name",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "grade_level",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "last_name",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "lrn",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "middle_name",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "strand_id",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "subjects",
                table: "enrollments");

            migrationBuilder.DropColumn(
                name: "track_id",
                table: "enrollments");

            // The school year, terms, strands and subjects stay (they are valid in the older schema too).
            // The tracks go back to what they were.
            migrationBuilder.Sql(
                "UPDATE `tracks` SET `code` = 'TVL', `name` = 'Technical-Vocational-Livelihood' " +
                "WHERE `id` = 2 AND `code` = 'TECH-PRO';");
            migrationBuilder.Sql(
                "INSERT INTO `tracks` (`id`, `code`, `name`, `is_active`, `created_at_utc`, `updated_at_utc`) " +
                "SELECT 3, 'SPORTS', 'Sports', 1, '2026-10-03 00:00:00', '2026-10-03 00:00:00' FROM DUAL " +
                "WHERE NOT EXISTS (SELECT 1 FROM `tracks` WHERE `id` = 3 OR `code` = 'SPORTS');");
            migrationBuilder.Sql(
                "INSERT INTO `tracks` (`id`, `code`, `name`, `is_active`, `created_at_utc`, `updated_at_utc`) " +
                "SELECT 4, 'ARTS', 'Arts and Design', 1, '2026-10-03 00:00:00', '2026-10-03 00:00:00' FROM DUAL " +
                "WHERE NOT EXISTS (SELECT 1 FROM `tracks` WHERE `id` = 4 OR `code` = 'ARTS');");
        }

        // ------------------------------------------------------------------------------------------------------------
        // The school's data. Names are written the way the school wrote them. Subject codes are short names made up
        // here (the school gave none); change them in the subjects table if you like, nothing depends on their text.
        // ------------------------------------------------------------------------------------------------------------

        private static readonly string[][] Strands =
        [
            // code, name, track
            ["BUSSENT", "BUSSENT", "ACAD"],
            ["EDUC", "EDUC", "ACAD"],
            ["ENG", "ENG", "ACAD"],
            ["MED", "MED", "ACAD"],
            ["CD1", "CSS-VGD", "TECH-PRO"],
            ["HT1", "KO-FBS", "TECH-PRO"],
            ["HT2", "FBS-KO", "TECH-PRO"],
            ["USP", "USP", "TECH-PRO"],
        ];

        private static readonly string[][] StrandSchedules =
        [
            // strand, schedules
            ["BUSSENT", "AM", "PM"],
            ["EDUC", "AM", "PM"],
            ["ENG", "AM", "PM"],
            ["MED", "AM", "PM"],
            ["CD1", "AM", "PM"],
            ["HT1", "AM", "MID", "PM"],
            ["HT2", "PM"],
            ["USP", "AM"],
        ];

        private static readonly string[][] Subjects =
        [
            // code, name
            ["BUSACCT", "BUSINESS ACCOUNTING"],
            ["ADVMATH1", "ADVANCE MATHEMATICS 1"],
            ["FIL1", "FILIPINO 1"],
            ["CREACOMP1", "CREATIVE COMPOSITION 1"],
            ["FINMATH1", "FINITE MATH 1"],
            ["EMPOWERTECH", "EMPOWERMENT TECHNOLOGY"],
            ["BIO1", "BIOLOGY 1"],
            ["SOCSCI", "SOCIAL SCIENCES"],
            ["CSS1", "COMPUTER SYSTEM SERVICING 1"],
            ["KITCHENOPS1", "KITCHEN OPERATION 1"],
            ["FBSOPS1", "FOOD AND BEVERAGE OPERATIONS 1"],
            ["EFFCOMM", "EFFECTIVE COMMUNICATION"],
            ["MABKOM", "MABISANG KOMUNIKASYON"],
            ["GENMATH", "GENERAL MATHEMATICS"],
            ["LIFECAREER", "LIFE AND CAREER SKILLS"],
            ["GENSCI", "GENERAL SCIENCE"],
            ["KASAYSAYAN", "PAG-AARAL NG KASAYSAYAN AT LIPUNANG PILIPINO"],
        ];

        /// <summary>Grade 11, 1st term: the subjects of each strand.</summary>
        private static readonly string[][] FirstTerm =
        [
            // strand, subjects
            ["BUSSENT", "BUSACCT", "ADVMATH1"],
            ["EDUC", "FIL1", "CREACOMP1"],
            ["ENG", "FINMATH1", "EMPOWERTECH"],
            ["MED", "BIO1", "EMPOWERTECH"],
            ["USP", "SOCSCI", "BIO1"],
            ["CD1", "CSS1"],
            ["HT1", "KITCHENOPS1"],
            ["HT2", "FBSOPS1"],
        ];

        /// <summary>Grade 11, 2nd term: the same six subjects for every strand.</summary>
        private static readonly string[] SecondTerm = ["EFFCOMM", "MABKOM", "GENMATH", "LIFECAREER", "GENSCI", "KASAYSAYAN"];

        private static string Q(string text) => "'" + text.Replace("'", "''") + "'";

        /// <summary>"SELECT 'a' AS `x`, 'b' AS `y` UNION ALL SELECT 'c', 'd'": a small table written inside a query.</summary>
        private static string Rows(string[] columnNames, IEnumerable<string[]> rows)
        {
            var parts = new List<string>();
            foreach (var row in rows)
            {
                var cells = row.Select((value, i) => parts.Count == 0 ? $"{Q(value)} AS `{columnNames[i]}`" : Q(value));
                parts.Add("SELECT " + string.Join(", ", cells));
            }

            return string.Join(" UNION ALL ", parts);
        }

        private static void SeedSchoolData(MigrationBuilder migrationBuilder)
        {
            // School year 2026-2027 and its two terms. The dates are a guess: change them in the school_years and terms tables.
            migrationBuilder.Sql(
                "INSERT INTO `school_years` (`name`, `start_date`, `end_date`, `created_at_utc`, `updated_at_utc`) " +
                "SELECT '2026-2027', '2026-06-08', '2027-03-31', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM DUAL " +
                "WHERE NOT EXISTS (SELECT 1 FROM `school_years` WHERE `name` = '2026-2027');");

            migrationBuilder.Sql(
                "INSERT INTO `terms` (`school_year_id`, `term_number`, `name`, `start_date`, `end_date`, `is_current`, `created_at_utc`, `updated_at_utc`) " +
                "SELECT sy.`id`, d.`term_number`, d.`name`, d.`start_date`, d.`end_date`, " +
                "IF(d.`term_number` = 1 AND NOT EXISTS (SELECT 1 FROM `terms` c WHERE c.`is_current` = 1), 1, 0), " +
                "UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) " +
                "FROM `school_years` sy JOIN (" +
                Rows(["term_number", "name", "start_date", "end_date"],
                [
                    ["1", "1st Term", "2026-06-08", "2026-10-30"],
                    ["2", "2nd Term", "2026-11-02", "2027-03-31"],
                ]) +
                ") d WHERE sy.`name` = '2026-2027' " +
                "AND NOT EXISTS (SELECT 1 FROM `terms` t WHERE t.`school_year_id` = sy.`id` AND t.`term_number` = d.`term_number`);");

            // AM, MID and PM.
            migrationBuilder.Sql(
                "INSERT INTO `schedules` (`code`, `name`, `created_at_utc`, `updated_at_utc`) " +
                "SELECT d.`code`, d.`name`, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM (" +
                Rows(["code", "name"], [["AM", "Morning"], ["MID", "Midday"], ["PM", "Afternoon"]]) +
                ") d WHERE NOT EXISTS (SELECT 1 FROM `schedules` s WHERE s.`code` = d.`code` OR s.`name` = d.`name`);");

            // The strands, each under its track.
            migrationBuilder.Sql(
                "INSERT INTO `strands` (`code`, `name`, `track_id`, `is_active`, `created_at_utc`, `updated_at_utc`) " +
                "SELECT d.`code`, d.`name`, t.`id`, 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM (" +
                Rows(["code", "name", "track_code"], Strands) +
                ") d JOIN `tracks` t ON t.`code` = d.`track_code` " +
                "WHERE NOT EXISTS (SELECT 1 FROM `strands` s WHERE s.`code` = d.`code`);");

            // Which schedules each strand runs in.
            migrationBuilder.Sql(
                "INSERT INTO `strand_schedules` (`strand_id`, `schedule_id`) " +
                "SELECT st.`id`, sc.`id` FROM (" +
                Rows(["strand_code", "schedule_code"], StrandSchedules.SelectMany(r => r.Skip(1).Select(s => new[] { r[0], s }))) +
                ") d JOIN `strands` st ON st.`code` = d.`strand_code` JOIN `schedules` sc ON sc.`code` = d.`schedule_code` " +
                "WHERE NOT EXISTS (SELECT 1 FROM `strand_schedules` x WHERE x.`strand_id` = st.`id` AND x.`schedule_id` = sc.`id`);");

            // The subjects.
            migrationBuilder.Sql(
                "INSERT INTO `subjects` (`code`, `name`, `is_active`, `created_at_utc`, `updated_at_utc`) " +
                "SELECT d.`code`, d.`name`, 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM (" +
                Rows(["code", "name"], Subjects) +
                ") d WHERE NOT EXISTS (SELECT 1 FROM `subjects` s WHERE s.`code` = d.`code`);");

            // The curriculum: which subjects each strand takes in grade 11, 1st and 2nd term.
            var curriculum = FirstTerm
                .SelectMany(r => r.Skip(1).Select(subject => new[] { r[0], "1", subject }))
                .Concat(Strands.SelectMany(s => SecondTerm.Select(subject => new[] { s[0], "2", subject })));

            migrationBuilder.Sql(
                "INSERT INTO `strand_subjects` (`strand_id`, `grade_level`, `term_number`, `subject_id`) " +
                "SELECT st.`id`, 11, d.`term_number`, sub.`id` FROM (" +
                Rows(["strand_code", "term_number", "subject_code"], curriculum) +
                ") d JOIN `strands` st ON st.`code` = d.`strand_code` JOIN `subjects` sub ON sub.`code` = d.`subject_code` " +
                "WHERE NOT EXISTS (SELECT 1 FROM `strand_subjects` x WHERE x.`strand_id` = st.`id` AND x.`grade_level` = 11 " +
                "AND x.`term_number` = d.`term_number` AND x.`subject_id` = sub.`id`);");
        }
    }
}
