using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SEAITT_LMS.Data.Migrations
{
    /// <summary>
    /// A person's name now lives in the table of their role (students, teachers, administrators, guidance_counselors,
    /// registrars) instead of in users, which keeps only the login: email, password, sign-in security.
    ///
    /// - first_name, middle_name, last_name and name_extension move from users into the five role tables.
    /// - administrators, guidance_counselors and registrars also get phone_number (contact number) and status
    ///   (Active/Inactive). They start as the person's users.phone_number and users.is_active.
    /// - The v_... views read users.first_name and friends, so they are dropped (the columns they showed are now real).
    ///
    /// Existing rows are kept: each name is copied before the old columns are dropped. The new columns are placed next
    /// to the name (AFTER ...) so the tables read well in Workbench.
    ///
    /// Raw SQL ends with ';' and every UPDATE has a WHERE on the primary key, so the generated scripts also run in
    /// MySQL Workbench with its default "Safe Updates" (see AddTracksAndRoleViews).
    /// </summary>
    public partial class MovePersonDetailsToRoleTables : Migration
    {
        private static readonly string[] RoleViews =
            ["v_students", "v_teachers", "v_registrars", "v_guidance_counselors", "v_administrators"];

        // (table, the column the name goes after, has contact number and status)
        private static readonly (string Table, string After, bool Contact)[] Tables =
        [
            ("administrators", "employee_number", true),
            ("guidance_counselors", "employee_number", true),
            ("registrars", "employee_number", true),
            ("teachers", "employee_number", false),
            ("students", "lrn", false),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var view in RoleViews)
            {
                migrationBuilder.Sql($"DROP VIEW IF EXISTS `{view}`;");
            }

            // New columns first (empty), so the data can be copied before anything is dropped.
            foreach (var (table, after, contact) in Tables)
            {
                migrationBuilder.Sql(AddColumns(table, after, contact));
            }

            foreach (var (table, _, contact) in Tables)
            {
                migrationBuilder.Sql(CopyFromUsers(table, contact));
            }

            foreach (var (table, _, contact) in Tables)
            {
                migrationBuilder.Sql(MakeRequired(table, contact));
            }

            // The staff tables were created with the two timestamp columns right after id. Move them to the end so the
            // tables read id, user_id, employee_number, first_name, ... (students already end with them).
            foreach (var (table, _, contact) in Tables.Where(t => t.Table != "students"))
            {
                var lastColumn = contact ? "status" : "name_extension";
                migrationBuilder.Sql($"""
                    ALTER TABLE `{table}`
                        MODIFY COLUMN `created_at_utc` datetime(6) NOT NULL AFTER `{lastColumn}`,
                        MODIFY COLUMN `updated_at_utc` datetime(6) NOT NULL AFTER `created_at_utc`;
                    """);
            }

            migrationBuilder.DropColumn(name: "first_name", table: "users");
            migrationBuilder.DropColumn(name: "middle_name", table: "users");
            migrationBuilder.DropColumn(name: "last_name", table: "users");
            migrationBuilder.DropColumn(name: "name_extension", table: "users");

            migrationBuilder.AddCheckConstraint(
                name: "ck_administrators_status",
                table: "administrators",
                sql: "status IN ('Active', 'Inactive')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_guidance_counselors_status",
                table: "guidance_counselors",
                sql: "status IN ('Active', 'Inactive')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_registrars_status",
                table: "registrars",
                sql: "status IN ('Active', 'Inactive')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(name: "ck_administrators_status", table: "administrators");
            migrationBuilder.DropCheckConstraint(name: "ck_guidance_counselors_status", table: "guidance_counselors");
            migrationBuilder.DropCheckConstraint(name: "ck_registrars_status", table: "registrars");

            // Put the name columns back in users (as before: right after phone_number), fill them from the role tables.
            migrationBuilder.Sql("""
                ALTER TABLE `users`
                    ADD COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `phone_number`,
                    ADD COLUMN `middle_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `first_name`,
                    ADD COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `middle_name`,
                    ADD COLUMN `name_extension` varchar(10) CHARACTER SET utf8mb4 NULL AFTER `last_name`;
                """);

            foreach (var (table, _, _) in Tables)
            {
                migrationBuilder.Sql($"""
                    UPDATE `users` u
                    JOIN `{table}` p ON p.`user_id` = u.`id`
                    SET u.`first_name` = p.`first_name`,
                        u.`middle_name` = p.`middle_name`,
                        u.`last_name` = p.`last_name`,
                        u.`name_extension` = p.`name_extension`
                    WHERE u.`id` > 0;
                    """);
            }

            // A user without any profile row (should not exist) gets an empty name instead of blocking the rollback.
            migrationBuilder.Sql("""
                UPDATE `users`
                SET `first_name` = '', `last_name` = ''
                WHERE `id` > 0 AND (`first_name` IS NULL OR `last_name` IS NULL);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE `users`
                    MODIFY COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    MODIFY COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL;
                """);

            foreach (var (table, _, contact) in Tables)
            {
                migrationBuilder.Sql(DropColumns(table, contact));
            }

            // The views the previous migration created (they read users.first_name and friends again).
            migrationBuilder.Sql(StaffView("v_administrators", "administrators"));
            migrationBuilder.Sql(StaffView("v_guidance_counselors", "guidance_counselors"));
            migrationBuilder.Sql(StaffView("v_registrars", "registrars"));
            migrationBuilder.Sql(TeachersView);
            migrationBuilder.Sql(StudentsView);
        }

        private static string AddColumns(string table, string after, bool contact)
        {
            var contactColumns = contact
                ? ",\n    ADD COLUMN `phone_number` varchar(20) CHARACTER SET utf8mb4 NULL AFTER `name_extension`" +
                  ",\n    ADD COLUMN `status` varchar(20) CHARACTER SET utf8mb4 NULL AFTER `phone_number`"
                : "";

            return $"""
                ALTER TABLE `{table}`
                    ADD COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `{after}`,
                    ADD COLUMN `middle_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `first_name`,
                    ADD COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `middle_name`,
                    ADD COLUMN `name_extension` varchar(10) CHARACTER SET utf8mb4 NULL AFTER `last_name`{contactColumns};
                """;
        }

        private static string CopyFromUsers(string table, bool contact)
        {
            var contactColumns = contact
                ? ",\n    p.`phone_number` = u.`phone_number`" +
                  ",\n    p.`status` = CASE WHEN u.`is_active` = 1 THEN 'Active' ELSE 'Inactive' END"
                : "";

            return $"""
                UPDATE `{table}` p
                JOIN `users` u ON u.`id` = p.`user_id`
                SET p.`first_name` = u.`first_name`,
                    p.`middle_name` = u.`middle_name`,
                    p.`last_name` = u.`last_name`,
                    p.`name_extension` = u.`name_extension`{contactColumns}
                WHERE p.`id` > 0;
                """;
        }

        private static string MakeRequired(string table, bool contact)
        {
            var contactColumns = contact
                ? ",\n    MODIFY COLUMN `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL"
                : "";

            return $"""
                ALTER TABLE `{table}`
                    MODIFY COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    MODIFY COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL{contactColumns};
                """;
        }

        private static string DropColumns(string table, bool contact)
        {
            var contactColumns = contact
                ? ",\n    DROP COLUMN `phone_number`,\n    DROP COLUMN `status`"
                : "";

            return $"""
                ALTER TABLE `{table}`
                    DROP COLUMN `first_name`,
                    DROP COLUMN `middle_name`,
                    DROP COLUMN `last_name`,
                    DROP COLUMN `name_extension`{contactColumns};
                """;
        }

        // ---- the views exactly as AddTracksAndRoleViews created them (frozen copies, used only by Down) -------------

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
