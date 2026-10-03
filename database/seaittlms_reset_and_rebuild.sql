-- =============================================================================
-- SEAITT e-LMS - RESET AND REBUILD the seaittlms database (MySQL Workbench friendly)
--
--   !!!  WARNING: THIS ERASES EVERYTHING IN THE seaittlms SCHEMA  !!!
--   Every table, view and row (users, students, teachers, ...) is deleted and the
--   database is built again from scratch. There is NO undo.
--
-- Use it only to start over, for example when tables were dropped or changed by hand and
-- Workbench shows "Error loading schema content ... Error Code: 1356".
-- To keep your data, do NOT run this file. Start the app instead (dotnet watch): it upgrades
-- the database and keeps everything.
--
-- Afterwards the database is empty, except the five roles and the four tracks.
-- Start the app to create the first administrator (README.md, "First-time setup").
--
-- HOW TO RUN IT IN MYSQL WORKBENCH
--   File > Open SQL Script...  pick this file, then click the lightning bolt (Execute).
--
-- This file is GENERATED from the Entity Framework migrations in Data/Migrations.
-- Do not edit it by hand. To regenerate it: bash database/export-schema.sh
-- =============================================================================

DROP DATABASE IF EXISTS `seaittlms`;
CREATE DATABASE `seaittlms` CHARACTER SET utf8mb4;
USE `seaittlms`;

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `migration_id` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `product_version` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `pk___ef_migrations_history` PRIMARY KEY (`migration_id`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;
ALTER DATABASE CHARACTER SET utf8mb4;

CREATE TABLE `roles` (
    `id` int NOT NULL,
    `name` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `pk_roles` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `school_years` (
    `id` int NOT NULL AUTO_INCREMENT,
    `name` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `start_date` date NOT NULL,
    `end_date` date NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_school_years` PRIMARY KEY (`id`),
    CONSTRAINT `ck_school_years_dates` CHECK (end_date > start_date)
) CHARACTER SET=utf8mb4;

CREATE TABLE `strands` (
    `id` int NOT NULL AUTO_INCREMENT,
    `code` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `name` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `track` varchar(50) CHARACTER SET utf8mb4 NULL,
    `is_active` tinyint(1) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_strands` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `subjects` (
    `id` int NOT NULL AUTO_INCREMENT,
    `code` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `name` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(500) CHARACTER SET utf8mb4 NULL,
    `is_active` tinyint(1) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_subjects` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `users` (
    `id` int NOT NULL AUTO_INCREMENT,
    `email` varchar(254) CHARACTER SET utf8mb4 NOT NULL,
    `password_hash` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `phone_number` varchar(20) CHARACTER SET utf8mb4 NULL,
    `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `middle_name` varchar(100) CHARACTER SET utf8mb4 NULL,
    `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `name_extension` varchar(10) CHARACTER SET utf8mb4 NULL,
    `role_id` int NOT NULL,
    `is_active` tinyint(1) NOT NULL,
    `security_stamp` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `failed_login_count` int NOT NULL,
    `lockout_end_utc` datetime(6) NULL,
    `last_login_at_utc` datetime(6) NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_users` PRIMARY KEY (`id`),
    CONSTRAINT `ck_users_failed_login_count` CHECK (failed_login_count >= 0),
    CONSTRAINT `fk_users_roles_role_id` FOREIGN KEY (`role_id`) REFERENCES `roles` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `terms` (
    `id` int NOT NULL AUTO_INCREMENT,
    `school_year_id` int NOT NULL,
    `term_number` tinyint unsigned NOT NULL,
    `name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `start_date` date NOT NULL,
    `end_date` date NOT NULL,
    `is_current` tinyint(1) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_terms` PRIMARY KEY (`id`),
    CONSTRAINT `ak_terms_id_school_year_id` UNIQUE (`id`, `school_year_id`),
    CONSTRAINT `ck_terms_dates` CHECK (end_date >= start_date),
    CONSTRAINT `ck_terms_term_number` CHECK (term_number >= 1),
    CONSTRAINT `fk_terms_school_years_school_year_id` FOREIGN KEY (`school_year_id`) REFERENCES `school_years` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `administrators` (
    `id` int NOT NULL AUTO_INCREMENT,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    `user_id` int NOT NULL,
    `employee_number` varchar(30) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `pk_administrators` PRIMARY KEY (`id`),
    CONSTRAINT `fk_administrators_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `guidance_counselors` (
    `id` int NOT NULL AUTO_INCREMENT,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    `user_id` int NOT NULL,
    `employee_number` varchar(30) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `pk_guidance_counselors` PRIMARY KEY (`id`),
    CONSTRAINT `fk_guidance_counselors_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `registrars` (
    `id` int NOT NULL AUTO_INCREMENT,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    `user_id` int NOT NULL,
    `employee_number` varchar(30) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `pk_registrars` PRIMARY KEY (`id`),
    CONSTRAINT `fk_registrars_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `students` (
    `id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,
    `lrn` char(12) CHARACTER SET utf8mb4 NOT NULL,
    `birth_date` date NULL,
    `sex` varchar(10) CHARACTER SET utf8mb4 NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_students` PRIMARY KEY (`id`),
    CONSTRAINT `ck_students_lrn` CHECK (lrn REGEXP '^[0-9]{12}$'),
    CONSTRAINT `fk_students_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `teachers` (
    `id` int NOT NULL AUTO_INCREMENT,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    `user_id` int NOT NULL,
    `employee_number` varchar(30) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `pk_teachers` PRIMARY KEY (`id`),
    CONSTRAINT `fk_teachers_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `sections` (
    `id` int NOT NULL AUTO_INCREMENT,
    `school_year_id` int NOT NULL,
    `strand_id` int NOT NULL,
    `grade_level` tinyint unsigned NOT NULL,
    `name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `adviser_teacher_id` int NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_sections` PRIMARY KEY (`id`),
    CONSTRAINT `ak_sections_id_school_year_id` UNIQUE (`id`, `school_year_id`),
    CONSTRAINT `ck_sections_grade_level` CHECK (grade_level IN (11, 12)),
    CONSTRAINT `fk_sections_school_years_school_year_id` FOREIGN KEY (`school_year_id`) REFERENCES `school_years` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_sections_strands_strand_id` FOREIGN KEY (`strand_id`) REFERENCES `strands` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_sections_teachers_adviser_teacher_id` FOREIGN KEY (`adviser_teacher_id`) REFERENCES `teachers` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `class_offerings` (
    `id` int NOT NULL AUTO_INCREMENT,
    `school_year_id` int NOT NULL,
    `term_id` int NOT NULL,
    `section_id` int NOT NULL,
    `subject_id` int NOT NULL,
    `teacher_id` int NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_class_offerings` PRIMARY KEY (`id`),
    CONSTRAINT `fk_class_offerings_sections_section_id_school_year_id` FOREIGN KEY (`section_id`, `school_year_id`) REFERENCES `sections` (`id`, `school_year_id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_class_offerings_subjects_subject_id` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_class_offerings_teachers_teacher_id` FOREIGN KEY (`teacher_id`) REFERENCES `teachers` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_class_offerings_terms_term_id_school_year_id` FOREIGN KEY (`term_id`, `school_year_id`) REFERENCES `terms` (`id`, `school_year_id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `enrollments` (
    `id` int NOT NULL AUTO_INCREMENT,
    `school_year_id` int NOT NULL,
    `term_id` int NOT NULL,
    `section_id` int NOT NULL,
    `student_id` int NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `enrolled_by_user_id` int NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_enrollments` PRIMARY KEY (`id`),
    CONSTRAINT `fk_enrollments_sections_section_id_school_year_id` FOREIGN KEY (`section_id`, `school_year_id`) REFERENCES `sections` (`id`, `school_year_id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_enrollments_students_student_id` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_enrollments_terms_term_id_school_year_id` FOREIGN KEY (`term_id`, `school_year_id`) REFERENCES `terms` (`id`, `school_year_id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_enrollments_users_enrolled_by_user_id` FOREIGN KEY (`enrolled_by_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

INSERT INTO `roles` (`id`, `name`)
VALUES (1, 'Admin'),
(2, 'Teacher'),
(3, 'Student'),
(4, 'Registrar'),
(5, 'GuidanceCounselor');

CREATE UNIQUE INDEX `ix_administrators_employee_number` ON `administrators` (`employee_number`);

CREATE UNIQUE INDEX `ix_administrators_user_id` ON `administrators` (`user_id`);

CREATE INDEX `ix_class_offerings_section_id_school_year_id` ON `class_offerings` (`section_id`, `school_year_id`);

CREATE INDEX `ix_class_offerings_subject_id` ON `class_offerings` (`subject_id`);

CREATE INDEX `ix_class_offerings_teacher_id` ON `class_offerings` (`teacher_id`);

CREATE INDEX `ix_class_offerings_term_id_school_year_id` ON `class_offerings` (`term_id`, `school_year_id`);

CREATE UNIQUE INDEX `ix_class_offerings_term_id_section_id_subject_id` ON `class_offerings` (`term_id`, `section_id`, `subject_id`);

CREATE INDEX `ix_enrollments_enrolled_by_user_id` ON `enrollments` (`enrolled_by_user_id`);

CREATE INDEX `ix_enrollments_section_id_school_year_id` ON `enrollments` (`section_id`, `school_year_id`);

CREATE UNIQUE INDEX `ix_enrollments_student_id_term_id` ON `enrollments` (`student_id`, `term_id`);

CREATE INDEX `ix_enrollments_term_id_school_year_id` ON `enrollments` (`term_id`, `school_year_id`);

CREATE UNIQUE INDEX `ix_guidance_counselors_employee_number` ON `guidance_counselors` (`employee_number`);

CREATE UNIQUE INDEX `ix_guidance_counselors_user_id` ON `guidance_counselors` (`user_id`);

CREATE UNIQUE INDEX `ix_registrars_employee_number` ON `registrars` (`employee_number`);

CREATE UNIQUE INDEX `ix_registrars_user_id` ON `registrars` (`user_id`);

CREATE UNIQUE INDEX `ix_roles_name` ON `roles` (`name`);

CREATE UNIQUE INDEX `ix_school_years_name` ON `school_years` (`name`);

CREATE INDEX `ix_sections_adviser_teacher_id` ON `sections` (`adviser_teacher_id`);

CREATE UNIQUE INDEX `ix_sections_school_year_id_strand_id_grade_level_name` ON `sections` (`school_year_id`, `strand_id`, `grade_level`, `name`);

CREATE INDEX `ix_sections_strand_id` ON `sections` (`strand_id`);

CREATE UNIQUE INDEX `ix_strands_code` ON `strands` (`code`);

CREATE UNIQUE INDEX `ix_students_lrn` ON `students` (`lrn`);

CREATE UNIQUE INDEX `ix_students_user_id` ON `students` (`user_id`);

CREATE UNIQUE INDEX `ix_subjects_code` ON `subjects` (`code`);

CREATE UNIQUE INDEX `ix_teachers_employee_number` ON `teachers` (`employee_number`);

CREATE UNIQUE INDEX `ix_teachers_user_id` ON `teachers` (`user_id`);

CREATE UNIQUE INDEX `ix_terms_school_year_id_term_number` ON `terms` (`school_year_id`, `term_number`);

CREATE UNIQUE INDEX `ix_users_email` ON `users` (`email`);

CREATE INDEX `ix_users_role_id` ON `users` (`role_id`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261003115912_InitialCreate', '9.0.20');

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
SET s.`track_id` = t.`id`
WHERE s.`id` > 0;

UPDATE `strands`
SET `track_id` = (SELECT `id` FROM `tracks` WHERE `code` = 'ACAD')
WHERE `id` > 0 AND `track_id` IS NULL;

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

DROP VIEW IF EXISTS `v_students`;

DROP VIEW IF EXISTS `v_teachers`;

DROP VIEW IF EXISTS `v_registrars`;

DROP VIEW IF EXISTS `v_guidance_counselors`;

DROP VIEW IF EXISTS `v_administrators`;

ALTER TABLE `administrators`
    ADD COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `employee_number`,
    ADD COLUMN `middle_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `first_name`,
    ADD COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `middle_name`,
    ADD COLUMN `name_extension` varchar(10) CHARACTER SET utf8mb4 NULL AFTER `last_name`,
    ADD COLUMN `phone_number` varchar(20) CHARACTER SET utf8mb4 NULL AFTER `name_extension`,
    ADD COLUMN `status` varchar(20) CHARACTER SET utf8mb4 NULL AFTER `phone_number`;

ALTER TABLE `guidance_counselors`
    ADD COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `employee_number`,
    ADD COLUMN `middle_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `first_name`,
    ADD COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `middle_name`,
    ADD COLUMN `name_extension` varchar(10) CHARACTER SET utf8mb4 NULL AFTER `last_name`,
    ADD COLUMN `phone_number` varchar(20) CHARACTER SET utf8mb4 NULL AFTER `name_extension`,
    ADD COLUMN `status` varchar(20) CHARACTER SET utf8mb4 NULL AFTER `phone_number`;

ALTER TABLE `registrars`
    ADD COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `employee_number`,
    ADD COLUMN `middle_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `first_name`,
    ADD COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `middle_name`,
    ADD COLUMN `name_extension` varchar(10) CHARACTER SET utf8mb4 NULL AFTER `last_name`,
    ADD COLUMN `phone_number` varchar(20) CHARACTER SET utf8mb4 NULL AFTER `name_extension`,
    ADD COLUMN `status` varchar(20) CHARACTER SET utf8mb4 NULL AFTER `phone_number`;

ALTER TABLE `teachers`
    ADD COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `employee_number`,
    ADD COLUMN `middle_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `first_name`,
    ADD COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `middle_name`,
    ADD COLUMN `name_extension` varchar(10) CHARACTER SET utf8mb4 NULL AFTER `last_name`;

ALTER TABLE `students`
    ADD COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `lrn`,
    ADD COLUMN `middle_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `first_name`,
    ADD COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NULL AFTER `middle_name`,
    ADD COLUMN `name_extension` varchar(10) CHARACTER SET utf8mb4 NULL AFTER `last_name`;

UPDATE `administrators` p
JOIN `users` u ON u.`id` = p.`user_id`
SET p.`first_name` = u.`first_name`,
    p.`middle_name` = u.`middle_name`,
    p.`last_name` = u.`last_name`,
    p.`name_extension` = u.`name_extension`,
    p.`phone_number` = u.`phone_number`,
    p.`status` = CASE WHEN u.`is_active` = 1 THEN 'Active' ELSE 'Inactive' END
WHERE p.`id` > 0;

UPDATE `guidance_counselors` p
JOIN `users` u ON u.`id` = p.`user_id`
SET p.`first_name` = u.`first_name`,
    p.`middle_name` = u.`middle_name`,
    p.`last_name` = u.`last_name`,
    p.`name_extension` = u.`name_extension`,
    p.`phone_number` = u.`phone_number`,
    p.`status` = CASE WHEN u.`is_active` = 1 THEN 'Active' ELSE 'Inactive' END
WHERE p.`id` > 0;

UPDATE `registrars` p
JOIN `users` u ON u.`id` = p.`user_id`
SET p.`first_name` = u.`first_name`,
    p.`middle_name` = u.`middle_name`,
    p.`last_name` = u.`last_name`,
    p.`name_extension` = u.`name_extension`,
    p.`phone_number` = u.`phone_number`,
    p.`status` = CASE WHEN u.`is_active` = 1 THEN 'Active' ELSE 'Inactive' END
WHERE p.`id` > 0;

UPDATE `teachers` p
JOIN `users` u ON u.`id` = p.`user_id`
SET p.`first_name` = u.`first_name`,
    p.`middle_name` = u.`middle_name`,
    p.`last_name` = u.`last_name`,
    p.`name_extension` = u.`name_extension`
WHERE p.`id` > 0;

UPDATE `students` p
JOIN `users` u ON u.`id` = p.`user_id`
SET p.`first_name` = u.`first_name`,
    p.`middle_name` = u.`middle_name`,
    p.`last_name` = u.`last_name`,
    p.`name_extension` = u.`name_extension`
WHERE p.`id` > 0;

ALTER TABLE `administrators`
    MODIFY COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    MODIFY COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    MODIFY COLUMN `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `guidance_counselors`
    MODIFY COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    MODIFY COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    MODIFY COLUMN `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `registrars`
    MODIFY COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    MODIFY COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    MODIFY COLUMN `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `teachers`
    MODIFY COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    MODIFY COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `students`
    MODIFY COLUMN `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    MODIFY COLUMN `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `administrators`
    MODIFY COLUMN `created_at_utc` datetime(6) NOT NULL AFTER `status`,
    MODIFY COLUMN `updated_at_utc` datetime(6) NOT NULL AFTER `created_at_utc`;

ALTER TABLE `guidance_counselors`
    MODIFY COLUMN `created_at_utc` datetime(6) NOT NULL AFTER `status`,
    MODIFY COLUMN `updated_at_utc` datetime(6) NOT NULL AFTER `created_at_utc`;

ALTER TABLE `registrars`
    MODIFY COLUMN `created_at_utc` datetime(6) NOT NULL AFTER `status`,
    MODIFY COLUMN `updated_at_utc` datetime(6) NOT NULL AFTER `created_at_utc`;

ALTER TABLE `teachers`
    MODIFY COLUMN `created_at_utc` datetime(6) NOT NULL AFTER `name_extension`,
    MODIFY COLUMN `updated_at_utc` datetime(6) NOT NULL AFTER `created_at_utc`;

ALTER TABLE `users` DROP COLUMN `first_name`;

ALTER TABLE `users` DROP COLUMN `middle_name`;

ALTER TABLE `users` DROP COLUMN `last_name`;

ALTER TABLE `users` DROP COLUMN `name_extension`;

ALTER TABLE `administrators` ADD CONSTRAINT `ck_administrators_status` CHECK (status IN ('Active', 'Inactive'));

ALTER TABLE `guidance_counselors` ADD CONSTRAINT `ck_guidance_counselors_status` CHECK (status IN ('Active', 'Inactive'));

ALTER TABLE `registrars` ADD CONSTRAINT `ck_registrars_status` CHECK (status IN ('Active', 'Inactive'));

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261003142005_MovePersonDetailsToRoleTables', '9.0.20');

ALTER TABLE `teachers`
    ADD COLUMN `grade_level` tinyint unsigned NULL AFTER `name_extension`,
    ADD COLUMN `track_id` int NULL AFTER `grade_level`,
    ADD COLUMN `strand_id` int NULL AFTER `track_id`,
    ADD COLUMN `subject_id` int NULL AFTER `strand_id`,
    ADD COLUMN `section_id` int NULL AFTER `subject_id`;

ALTER TABLE `students`
    ADD COLUMN `grade_level` tinyint unsigned NULL AFTER `sex`,
    ADD COLUMN `track_id` int NULL AFTER `grade_level`,
    ADD COLUMN `strand_id` int NULL AFTER `track_id`,
    ADD COLUMN `subject_id` int NULL AFTER `strand_id`,
    ADD COLUMN `section_id` int NULL AFTER `subject_id`;

ALTER TABLE `strands` ADD CONSTRAINT `ak_strands_id_track_id` UNIQUE (`id`, `track_id`);

ALTER TABLE `sections` ADD CONSTRAINT `ak_sections_id_strand_id_grade_level` UNIQUE (`id`, `strand_id`, `grade_level`);

CREATE INDEX `ix_teachers_section_id_strand_id_grade_level` ON `teachers` (`section_id`, `strand_id`, `grade_level`);

CREATE INDEX `ix_teachers_strand_id_track_id` ON `teachers` (`strand_id`, `track_id`);

CREATE INDEX `ix_teachers_subject_id` ON `teachers` (`subject_id`);

CREATE INDEX `ix_teachers_track_id` ON `teachers` (`track_id`);

ALTER TABLE `teachers` ADD CONSTRAINT `ck_teachers_grade_level` CHECK (grade_level IS NULL OR grade_level IN (11, 12));

ALTER TABLE `teachers` ADD CONSTRAINT `ck_teachers_section_needs_strand_and_grade` CHECK (section_id IS NULL OR (strand_id IS NOT NULL AND grade_level IS NOT NULL));

ALTER TABLE `teachers` ADD CONSTRAINT `ck_teachers_strand_needs_track` CHECK (strand_id IS NULL OR track_id IS NOT NULL);

CREATE INDEX `ix_students_section_id_strand_id_grade_level` ON `students` (`section_id`, `strand_id`, `grade_level`);

CREATE INDEX `ix_students_strand_id_track_id` ON `students` (`strand_id`, `track_id`);

CREATE INDEX `ix_students_subject_id` ON `students` (`subject_id`);

CREATE INDEX `ix_students_track_id` ON `students` (`track_id`);

ALTER TABLE `students` ADD CONSTRAINT `ck_students_grade_level` CHECK (grade_level IS NULL OR grade_level IN (11, 12));

ALTER TABLE `students` ADD CONSTRAINT `ck_students_section_needs_strand_and_grade` CHECK (section_id IS NULL OR (strand_id IS NOT NULL AND grade_level IS NOT NULL));

ALTER TABLE `students` ADD CONSTRAINT `ck_students_strand_needs_track` CHECK (strand_id IS NULL OR track_id IS NOT NULL);

ALTER TABLE `students` ADD CONSTRAINT `fk_students_sections_section_id_strand_id_grade_level` FOREIGN KEY (`section_id`, `strand_id`, `grade_level`) REFERENCES `sections` (`id`, `strand_id`, `grade_level`) ON DELETE RESTRICT;

ALTER TABLE `students` ADD CONSTRAINT `fk_students_strands_strand_id_track_id` FOREIGN KEY (`strand_id`, `track_id`) REFERENCES `strands` (`id`, `track_id`) ON DELETE RESTRICT;

ALTER TABLE `students` ADD CONSTRAINT `fk_students_subjects_subject_id` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`) ON DELETE RESTRICT;

ALTER TABLE `students` ADD CONSTRAINT `fk_students_tracks_track_id` FOREIGN KEY (`track_id`) REFERENCES `tracks` (`id`) ON DELETE RESTRICT;

ALTER TABLE `teachers` ADD CONSTRAINT `fk_teachers_sections_section_id_strand_id_grade_level` FOREIGN KEY (`section_id`, `strand_id`, `grade_level`) REFERENCES `sections` (`id`, `strand_id`, `grade_level`) ON DELETE RESTRICT;

ALTER TABLE `teachers` ADD CONSTRAINT `fk_teachers_strands_strand_id_track_id` FOREIGN KEY (`strand_id`, `track_id`) REFERENCES `strands` (`id`, `track_id`) ON DELETE RESTRICT;

ALTER TABLE `teachers` ADD CONSTRAINT `fk_teachers_subjects_subject_id` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`) ON DELETE RESTRICT;

ALTER TABLE `teachers` ADD CONSTRAINT `fk_teachers_tracks_track_id` FOREIGN KEY (`track_id`) REFERENCES `tracks` (`id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261003142335_AddPlacementToStudentsAndTeachers', '9.0.20');

COMMIT;

