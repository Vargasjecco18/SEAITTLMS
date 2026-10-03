using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SEAITT_LMS.Data.Migrations
{
    /// <summary>
    /// 1. Adds the tracks table (seeded with the four DepEd senior high school tracks) and makes every strand belong
    ///    to one track. The old free-text strands.track column is converted, not thrown away.
    /// 2. Adds one read-only view per role (v_students, v_teachers, v_administrators, v_guidance_counselors,
    ///    v_registrars) that shows each person with their name, phone, status and current class details.
    ///    The views only read the real tables, nothing is stored twice.
    ///
    /// If a later migration renames or drops a column used by one of these views, drop and re-create that view in the
    /// same migration (MySQL does not update a view when its tables change).
    ///
    /// Every raw SQL statement below ends with a semicolon on purpose: EF copies raw SQL into the generated .sql
    /// scripts as it is, so without the semicolon two statements run together and the script fails in Workbench.
    /// </summary>
    public partial class AddTracksAndRoleViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---- tracks -------------------------------------------------------------------------------------------
            migrationBuilder.CreateTable(
                name: "tracks",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tracks", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "tracks",
                columns: new[] { "id", "code", "created_at_utc", "is_active", "name", "updated_at_utc" },
                values: new object[,]
                {
                    { 1, "ACAD", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, "Academic", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "TVL", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, "Technical-Vocational-Livelihood", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "SPORTS", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, "Sports", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "ARTS", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc), true, "Arts and Design", new DateTime(2026, 10, 3, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_tracks_code",
                table: "tracks",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tracks_name",
                table: "tracks",
                column: "name",
                unique: true);

            // ---- strands.track (text) -> strands.track_id (link to tracks), keeping existing rows ------------------
            migrationBuilder.AddColumn<int>(
                name: "track_id",
                table: "strands",
                type: "int",
                nullable: true);

            // The old text matched a track's code or name ("Academic", "TVL", "Sports", "Arts and Design").
            migrationBuilder.Sql("""
                UPDATE `strands` s
                JOIN `tracks` t ON LOWER(TRIM(s.`track`)) IN (LOWER(t.`code`), LOWER(t.`name`))
                SET s.`track_id` = t.`id`;
                """);

            // A strand whose old text was empty or not recognised goes under Academic. Fix it in the table if that is wrong.
            migrationBuilder.Sql("""
                UPDATE `strands`
                SET `track_id` = (SELECT `id` FROM `tracks` WHERE `code` = 'ACAD')
                WHERE `track_id` IS NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "track_id",
                table: "strands",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_strands_track_id",
                table: "strands",
                column: "track_id");

            migrationBuilder.AddForeignKey(
                name: "fk_strands_tracks_track_id",
                table: "strands",
                column: "track_id",
                principalTable: "tracks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "track",
                table: "strands");

            // ---- one view per role ----------------------------------------------------------------------------------
            migrationBuilder.Sql(StaffView("v_administrators", "administrators"));
            migrationBuilder.Sql(StaffView("v_guidance_counselors", "guidance_counselors"));
            migrationBuilder.Sql(StaffView("v_registrars", "registrars"));
            migrationBuilder.Sql(TeachersView);
            migrationBuilder.Sql(StudentsView);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS `v_students`;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS `v_teachers`;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS `v_registrars`;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS `v_guidance_counselors`;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS `v_administrators`;");

            // Put the text column back and fill it from the track the strand points to.
            migrationBuilder.AddColumn<string>(
                name: "track",
                table: "strands",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                UPDATE `strands` s
                JOIN `tracks` t ON t.`id` = s.`track_id`
                SET s.`track` = t.`name`;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_strands_tracks_track_id",
                table: "strands");

            migrationBuilder.DropIndex(
                name: "ix_strands_track_id",
                table: "strands");

            migrationBuilder.DropColumn(
                name: "track_id",
                table: "strands");

            migrationBuilder.DropTable(
                name: "tracks");
        }

        // The views run with the rights of whoever queries them (SQL SECURITY INVOKER), so they keep working if the
        // MySQL user that created them is later removed.

        /// <summary>
        /// Administrators, guidance counselors and registrars: name, phone and status come from the login account.
        /// Status is Active/Inactive, the same switch that lets the person sign in (users.is_active).
        /// </summary>
        private static string StaffView(string view, string table) => $"""
            CREATE SQL SECURITY INVOKER VIEW `{view}` AS
            SELECT
                p.`id`              AS `id`,
                p.`user_id`         AS `user_id`,
                p.`employee_number` AS `employee_number`,
                u.`first_name`      AS `first_name`,
                u.`middle_name`     AS `middle_name`,
                u.`last_name`       AS `last_name`,
                u.`phone_number`    AS `phone_number`,
                CASE WHEN u.`is_active` = 1 THEN 'Active' ELSE 'Inactive' END AS `status`
            FROM `{table}` p
            JOIN `users` u ON u.`id` = p.`user_id`;
            """;

        /// <summary>
        /// A teacher with what they teach in the current term (the term with is_current = 1), taken from
        /// class_offerings. A teacher can have several classes, so each column is a list.
        /// A section is shown as grade-strand-name, for example 11-STEM-A.
        /// </summary>
        private const string TeachersView = """
            CREATE SQL SECURITY INVOKER VIEW `v_teachers` AS
            SELECT
                t.`id`              AS `id`,
                t.`user_id`         AS `user_id`,
                t.`employee_number` AS `employee_number`,
                u.`first_name`      AS `first_name`,
                u.`middle_name`     AS `middle_name`,
                u.`last_name`       AS `last_name`,
                GROUP_CONCAT(DISTINCT sec.`grade_level` ORDER BY sec.`grade_level` SEPARATOR ', ') AS `grade_levels`,
                GROUP_CONCAT(DISTINCT tr.`name` ORDER BY tr.`name` SEPARATOR ', ') AS `tracks`,
                GROUP_CONCAT(DISTINCT st.`code` ORDER BY st.`code` SEPARATOR ', ') AS `strands`,
                GROUP_CONCAT(DISTINCT sub.`name` ORDER BY sub.`name` SEPARATOR ', ') AS `subjects`,
                GROUP_CONCAT(DISTINCT CONCAT(sec.`grade_level`, '-', st.`code`, '-', sec.`name`)
                             ORDER BY sec.`grade_level`, st.`code`, sec.`name` SEPARATOR ', ') AS `sections`
            FROM `teachers` t
            JOIN `users` u ON u.`id` = t.`user_id`
            LEFT JOIN `class_offerings` co ON co.`teacher_id` = t.`id`
                AND co.`term_id` = (SELECT cur.`id` FROM `terms` cur WHERE cur.`is_current` = 1 ORDER BY cur.`id` DESC LIMIT 1)
            LEFT JOIN `sections` sec ON sec.`id` = co.`section_id`
            LEFT JOIN `strands` st ON st.`id` = sec.`strand_id`
            LEFT JOIN `tracks` tr ON tr.`id` = st.`track_id`
            LEFT JOIN `subjects` sub ON sub.`id` = co.`subject_id`
            GROUP BY t.`id`, t.`user_id`, t.`employee_number`, u.`first_name`, u.`middle_name`, u.`last_name`;
            """;

        /// <summary>
        /// A student with the placement from their enrollment in the current term (status Enrolled): grade level, track,
        /// strand and section, plus the subjects of that section's classes. All empty when the student is not
        /// enrolled in the current term.
        /// </summary>
        private const string StudentsView = """
            CREATE SQL SECURITY INVOKER VIEW `v_students` AS
            SELECT
                s.`id`              AS `id`,
                s.`user_id`         AS `user_id`,
                s.`lrn`             AS `lrn`,
                u.`first_name`      AS `first_name`,
                u.`middle_name`     AS `middle_name`,
                u.`last_name`       AS `last_name`,
                sec.`grade_level`   AS `grade_level`,
                tr.`name`           AS `track`,
                st.`code`           AS `strand`,
                (SELECT GROUP_CONCAT(sub.`name` ORDER BY sub.`name` SEPARATOR ', ')
                   FROM `class_offerings` co
                   JOIN `subjects` sub ON sub.`id` = co.`subject_id`
                  WHERE co.`term_id` = e.`term_id` AND co.`section_id` = e.`section_id`) AS `subjects`,
                CONCAT(sec.`grade_level`, '-', st.`code`, '-', sec.`name`) AS `section`
            FROM `students` s
            JOIN `users` u ON u.`id` = s.`user_id`
            LEFT JOIN `enrollments` e ON e.`student_id` = s.`id`
                AND e.`status` = 'Enrolled'
                AND e.`term_id` = (SELECT cur.`id` FROM `terms` cur WHERE cur.`is_current` = 1 ORDER BY cur.`id` DESC LIMIT 1)
            LEFT JOIN `sections` sec ON sec.`id` = e.`section_id`
            LEFT JOIN `strands` st ON st.`id` = sec.`strand_id`
            LEFT JOIN `tracks` tr ON tr.`id` = st.`track_id`;
            """;
    }
}
