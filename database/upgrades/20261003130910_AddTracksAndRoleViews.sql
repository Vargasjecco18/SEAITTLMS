-- =============================================================================
-- SEAITT e-LMS - UPGRADE script for MySQL 8 (MySQL Workbench friendly)
--
-- Brings a database that already has the migration
--     20261003115912_InitialCreate
-- up to
--     20261003130910_AddTracksAndRoleViews
-- Your existing data is kept.
--
-- This file is GENERATED from the Entity Framework migrations in Data/Migrations.
-- Do not edit it by hand. To regenerate it: bash database/export-schema.sh
--
-- USE IT ONLY IF the last row of this query is 20261003115912_InitialCreate:
--     SELECT * FROM __EFMigrationsHistory ORDER BY migration_id;
-- (If the last row is already 20261003130910_AddTracksAndRoleViews or newer, you don't need this file. If it is older,
-- run the files in database/upgrades/ one by one, oldest first.)
--
-- HOW TO RUN IT IN MYSQL WORKBENCH
--   File > Open SQL Script...  pick this file, then click the lightning bolt (Execute).
--   Run it ONCE. It also records the upgrade in __EFMigrationsHistory.
--
-- If you start the app in Development instead, it applies these upgrades by itself.
-- =============================================================================

USE `seaittlms`;

START TRANSACTION;
CREATE TABLE `tracks` (
    `id` int NOT NULL AUTO_INCREMENT,
    `code` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `is_active` tinyint(1) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_tracks` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

INSERT INTO `tracks` (`id`, `code`, `created_at_utc`, `is_active`, `name`, `updated_at_utc`)
VALUES (1, 'ACAD', TIMESTAMP '2026-10-03 00:00:00', TRUE, 'Academic', TIMESTAMP '2026-10-03 00:00:00'),
(2, 'TVL', TIMESTAMP '2026-10-03 00:00:00', TRUE, 'Technical-Vocational-Livelihood', TIMESTAMP '2026-10-03 00:00:00'),
(3, 'SPORTS', TIMESTAMP '2026-10-03 00:00:00', TRUE, 'Sports', TIMESTAMP '2026-10-03 00:00:00'),
(4, 'ARTS', TIMESTAMP '2026-10-03 00:00:00', TRUE, 'Arts and Design', TIMESTAMP '2026-10-03 00:00:00');

CREATE UNIQUE INDEX `ix_tracks_code` ON `tracks` (`code`);

CREATE UNIQUE INDEX `ix_tracks_name` ON `tracks` (`name`);

ALTER TABLE `strands` ADD `track_id` int NULL;

UPDATE `strands` s
JOIN `tracks` t ON LOWER(TRIM(s.`track`)) IN (LOWER(t.`code`), LOWER(t.`name`))
SET s.`track_id` = t.`id`;

UPDATE `strands`
SET `track_id` = (SELECT `id` FROM `tracks` WHERE `code` = 'ACAD')
WHERE `track_id` IS NULL;

ALTER TABLE `strands` MODIFY COLUMN `track_id` int NOT NULL;

CREATE INDEX `ix_strands_track_id` ON `strands` (`track_id`);

ALTER TABLE `strands` ADD CONSTRAINT `fk_strands_tracks_track_id` FOREIGN KEY (`track_id`) REFERENCES `tracks` (`id`) ON DELETE RESTRICT;

ALTER TABLE `strands` DROP COLUMN `track`;

CREATE SQL SECURITY INVOKER VIEW `v_administrators` AS
SELECT
    p.`id`              AS `id`,
    p.`user_id`         AS `user_id`,
    p.`employee_number` AS `employee_number`,
    u.`first_name`      AS `first_name`,
    u.`middle_name`     AS `middle_name`,
    u.`last_name`       AS `last_name`,
    u.`phone_number`    AS `phone_number`,
    CASE WHEN u.`is_active` = 1 THEN 'Active' ELSE 'Inactive' END AS `status`
FROM `administrators` p
JOIN `users` u ON u.`id` = p.`user_id`;

CREATE SQL SECURITY INVOKER VIEW `v_guidance_counselors` AS
SELECT
    p.`id`              AS `id`,
    p.`user_id`         AS `user_id`,
    p.`employee_number` AS `employee_number`,
    u.`first_name`      AS `first_name`,
    u.`middle_name`     AS `middle_name`,
    u.`last_name`       AS `last_name`,
    u.`phone_number`    AS `phone_number`,
    CASE WHEN u.`is_active` = 1 THEN 'Active' ELSE 'Inactive' END AS `status`
FROM `guidance_counselors` p
JOIN `users` u ON u.`id` = p.`user_id`;

CREATE SQL SECURITY INVOKER VIEW `v_registrars` AS
SELECT
    p.`id`              AS `id`,
    p.`user_id`         AS `user_id`,
    p.`employee_number` AS `employee_number`,
    u.`first_name`      AS `first_name`,
    u.`middle_name`     AS `middle_name`,
    u.`last_name`       AS `last_name`,
    u.`phone_number`    AS `phone_number`,
    CASE WHEN u.`is_active` = 1 THEN 'Active' ELSE 'Inactive' END AS `status`
FROM `registrars` p
JOIN `users` u ON u.`id` = p.`user_id`;

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

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261003130910_AddTracksAndRoleViews', '9.0.20');

COMMIT;

