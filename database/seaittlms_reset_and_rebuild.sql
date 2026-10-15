-- =============================================================================
-- SEAITT Legazpi LMS - RESET AND REBUILD the seaittlms database (MySQL Workbench friendly)
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
-- Afterwards the database is empty, except the five roles and the school setup (school year
-- 2026-2027 with its two terms, the ACAD and TECH-PRO tracks and their strands, the schedules
-- AM, MID and PM, the subjects and the grade 11 curriculum).
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

UPDATE `tracks` SET `code` = 'TECH-PRO', `name` = 'Technical-Professional' WHERE `id` = 2 AND `code` = 'TVL' AND NOT EXISTS (SELECT 1 FROM (SELECT `id` FROM `tracks` WHERE `code` = 'TECH-PRO' OR `name` = 'Technical-Professional') AS `taken`);

DELETE FROM `tracks` WHERE `id` IN (3, 4) AND `code` IN ('SPORTS', 'ARTS') AND NOT EXISTS (SELECT 1 FROM `strands` WHERE `strands`.`track_id` = `tracks`.`id`) AND NOT EXISTS (SELECT 1 FROM `students` WHERE `students`.`track_id` = `tracks`.`id`) AND NOT EXISTS (SELECT 1 FROM `teachers` WHERE `teachers`.`track_id` = `tracks`.`id`);

UPDATE `tracks` SET `is_active` = 0 WHERE `id` IN (3, 4) AND `code` IN ('SPORTS', 'ARTS');

ALTER TABLE `sections` ADD COLUMN `schedule_id` int NULL AFTER `grade_level`;

ALTER TABLE `enrollments` ADD COLUMN `lrn` char(12) NULL AFTER `student_id`, ADD COLUMN `first_name` varchar(100) NULL AFTER `lrn`, ADD COLUMN `middle_name` varchar(100) NULL AFTER `first_name`, ADD COLUMN `last_name` varchar(100) NULL AFTER `middle_name`, ADD COLUMN `grade_level` tinyint unsigned NULL AFTER `last_name`, ADD COLUMN `track_id` int NULL AFTER `grade_level`, ADD COLUMN `strand_id` int NULL AFTER `track_id`, ADD COLUMN `subjects` varchar(1000) NULL AFTER `strand_id`;

CREATE TABLE `enrollment_subjects` (
    `enrollment_id` int NOT NULL,
    `subject_id` int NOT NULL,
    CONSTRAINT `pk_enrollment_subjects` PRIMARY KEY (`enrollment_id`, `subject_id`),
    CONSTRAINT `fk_enrollment_subjects_enrollments_enrollment_id` FOREIGN KEY (`enrollment_id`) REFERENCES `enrollments` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_enrollment_subjects_subjects_subject_id` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `schedules` (
    `id` int NOT NULL AUTO_INCREMENT,
    `code` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `name` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_schedules` PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;

CREATE TABLE `strand_subjects` (
    `strand_id` int NOT NULL,
    `grade_level` tinyint unsigned NOT NULL,
    `term_number` tinyint unsigned NOT NULL,
    `subject_id` int NOT NULL,
    CONSTRAINT `pk_strand_subjects` PRIMARY KEY (`strand_id`, `grade_level`, `term_number`, `subject_id`),
    CONSTRAINT `ck_strand_subjects_grade_level` CHECK (grade_level IN (11, 12)),
    CONSTRAINT `ck_strand_subjects_term_number` CHECK (term_number >= 1),
    CONSTRAINT `fk_strand_subjects_strands_strand_id` FOREIGN KEY (`strand_id`) REFERENCES `strands` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_strand_subjects_subjects_subject_id` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE `strand_schedules` (
    `strand_id` int NOT NULL,
    `schedule_id` int NOT NULL,
    CONSTRAINT `pk_strand_schedules` PRIMARY KEY (`strand_id`, `schedule_id`),
    CONSTRAINT `fk_strand_schedules_schedules_schedule_id` FOREIGN KEY (`schedule_id`) REFERENCES `schedules` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_strand_schedules_strands_strand_id` FOREIGN KEY (`strand_id`) REFERENCES `strands` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

INSERT INTO `school_years` (`name`, `start_date`, `end_date`, `created_at_utc`, `updated_at_utc`) SELECT '2026-2027', '2026-06-08', '2027-03-31', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `school_years` WHERE `name` = '2026-2027');

INSERT INTO `terms` (`school_year_id`, `term_number`, `name`, `start_date`, `end_date`, `is_current`, `created_at_utc`, `updated_at_utc`) SELECT sy.`id`, d.`term_number`, d.`name`, d.`start_date`, d.`end_date`, IF(d.`term_number` = 1 AND NOT EXISTS (SELECT 1 FROM `terms` c WHERE c.`is_current` = 1), 1, 0), UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM `school_years` sy JOIN (SELECT '1' AS `term_number`, '1st Term' AS `name`, '2026-06-08' AS `start_date`, '2026-10-30' AS `end_date` UNION ALL SELECT '2', '2nd Term', '2026-11-02', '2027-03-31') d WHERE sy.`name` = '2026-2027' AND NOT EXISTS (SELECT 1 FROM `terms` t WHERE t.`school_year_id` = sy.`id` AND t.`term_number` = d.`term_number`);

INSERT INTO `schedules` (`code`, `name`, `created_at_utc`, `updated_at_utc`) SELECT d.`code`, d.`name`, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM (SELECT 'AM' AS `code`, 'Morning' AS `name` UNION ALL SELECT 'MID', 'Midday' UNION ALL SELECT 'PM', 'Afternoon') d WHERE NOT EXISTS (SELECT 1 FROM `schedules` s WHERE s.`code` = d.`code` OR s.`name` = d.`name`);

INSERT INTO `strands` (`code`, `name`, `track_id`, `is_active`, `created_at_utc`, `updated_at_utc`) SELECT d.`code`, d.`name`, t.`id`, 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM (SELECT 'BUSSENT' AS `code`, 'BUSSENT' AS `name`, 'ACAD' AS `track_code` UNION ALL SELECT 'EDUC', 'EDUC', 'ACAD' UNION ALL SELECT 'ENG', 'ENG', 'ACAD' UNION ALL SELECT 'MED', 'MED', 'ACAD' UNION ALL SELECT 'CD1', 'CSS-VGD', 'TECH-PRO' UNION ALL SELECT 'HT1', 'KO-FBS', 'TECH-PRO' UNION ALL SELECT 'HT2', 'FBS-KO', 'TECH-PRO' UNION ALL SELECT 'USP', 'USP', 'TECH-PRO') d JOIN `tracks` t ON t.`code` = d.`track_code` WHERE NOT EXISTS (SELECT 1 FROM `strands` s WHERE s.`code` = d.`code`);

INSERT INTO `strand_schedules` (`strand_id`, `schedule_id`) SELECT st.`id`, sc.`id` FROM (SELECT 'BUSSENT' AS `strand_code`, 'AM' AS `schedule_code` UNION ALL SELECT 'BUSSENT', 'PM' UNION ALL SELECT 'EDUC', 'AM' UNION ALL SELECT 'EDUC', 'PM' UNION ALL SELECT 'ENG', 'AM' UNION ALL SELECT 'ENG', 'PM' UNION ALL SELECT 'MED', 'AM' UNION ALL SELECT 'MED', 'PM' UNION ALL SELECT 'CD1', 'AM' UNION ALL SELECT 'CD1', 'PM' UNION ALL SELECT 'HT1', 'AM' UNION ALL SELECT 'HT1', 'MID' UNION ALL SELECT 'HT1', 'PM' UNION ALL SELECT 'HT2', 'PM' UNION ALL SELECT 'USP', 'AM') d JOIN `strands` st ON st.`code` = d.`strand_code` JOIN `schedules` sc ON sc.`code` = d.`schedule_code` WHERE NOT EXISTS (SELECT 1 FROM `strand_schedules` x WHERE x.`strand_id` = st.`id` AND x.`schedule_id` = sc.`id`);

INSERT INTO `subjects` (`code`, `name`, `is_active`, `created_at_utc`, `updated_at_utc`) SELECT d.`code`, d.`name`, 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM (SELECT 'BUSACCT' AS `code`, 'BUSINESS ACCOUNTING' AS `name` UNION ALL SELECT 'ADVMATH1', 'ADVANCE MATHEMATICS 1' UNION ALL SELECT 'FIL1', 'FILIPINO 1' UNION ALL SELECT 'CREACOMP1', 'CREATIVE COMPOSITION 1' UNION ALL SELECT 'FINMATH1', 'FINITE MATH 1' UNION ALL SELECT 'EMPOWERTECH', 'EMPOWERMENT TECHNOLOGY' UNION ALL SELECT 'BIO1', 'BIOLOGY 1' UNION ALL SELECT 'SOCSCI', 'SOCIAL SCIENCES' UNION ALL SELECT 'CSS1', 'COMPUTER SYSTEM SERVICING 1' UNION ALL SELECT 'KITCHENOPS1', 'KITCHEN OPERATION 1' UNION ALL SELECT 'FBSOPS1', 'FOOD AND BEVERAGE OPERATIONS 1' UNION ALL SELECT 'EFFCOMM', 'EFFECTIVE COMMUNICATION' UNION ALL SELECT 'MABKOM', 'MABISANG KOMUNIKASYON' UNION ALL SELECT 'GENMATH', 'GENERAL MATHEMATICS' UNION ALL SELECT 'LIFECAREER', 'LIFE AND CAREER SKILLS' UNION ALL SELECT 'GENSCI', 'GENERAL SCIENCE' UNION ALL SELECT 'KASAYSAYAN', 'PAG-AARAL NG KASAYSAYAN AT LIPUNANG PILIPINO') d WHERE NOT EXISTS (SELECT 1 FROM `subjects` s WHERE s.`code` = d.`code`);

INSERT INTO `strand_subjects` (`strand_id`, `grade_level`, `term_number`, `subject_id`) SELECT st.`id`, 11, d.`term_number`, sub.`id` FROM (SELECT 'BUSSENT' AS `strand_code`, '1' AS `term_number`, 'BUSACCT' AS `subject_code` UNION ALL SELECT 'BUSSENT', '1', 'ADVMATH1' UNION ALL SELECT 'EDUC', '1', 'FIL1' UNION ALL SELECT 'EDUC', '1', 'CREACOMP1' UNION ALL SELECT 'ENG', '1', 'FINMATH1' UNION ALL SELECT 'ENG', '1', 'EMPOWERTECH' UNION ALL SELECT 'MED', '1', 'BIO1' UNION ALL SELECT 'MED', '1', 'EMPOWERTECH' UNION ALL SELECT 'USP', '1', 'SOCSCI' UNION ALL SELECT 'USP', '1', 'BIO1' UNION ALL SELECT 'CD1', '1', 'CSS1' UNION ALL SELECT 'HT1', '1', 'KITCHENOPS1' UNION ALL SELECT 'HT2', '1', 'FBSOPS1' UNION ALL SELECT 'BUSSENT', '2', 'EFFCOMM' UNION ALL SELECT 'BUSSENT', '2', 'MABKOM' UNION ALL SELECT 'BUSSENT', '2', 'GENMATH' UNION ALL SELECT 'BUSSENT', '2', 'LIFECAREER' UNION ALL SELECT 'BUSSENT', '2', 'GENSCI' UNION ALL SELECT 'BUSSENT', '2', 'KASAYSAYAN' UNION ALL SELECT 'EDUC', '2', 'EFFCOMM' UNION ALL SELECT 'EDUC', '2', 'MABKOM' UNION ALL SELECT 'EDUC', '2', 'GENMATH' UNION ALL SELECT 'EDUC', '2', 'LIFECAREER' UNION ALL SELECT 'EDUC', '2', 'GENSCI' UNION ALL SELECT 'EDUC', '2', 'KASAYSAYAN' UNION ALL SELECT 'ENG', '2', 'EFFCOMM' UNION ALL SELECT 'ENG', '2', 'MABKOM' UNION ALL SELECT 'ENG', '2', 'GENMATH' UNION ALL SELECT 'ENG', '2', 'LIFECAREER' UNION ALL SELECT 'ENG', '2', 'GENSCI' UNION ALL SELECT 'ENG', '2', 'KASAYSAYAN' UNION ALL SELECT 'MED', '2', 'EFFCOMM' UNION ALL SELECT 'MED', '2', 'MABKOM' UNION ALL SELECT 'MED', '2', 'GENMATH' UNION ALL SELECT 'MED', '2', 'LIFECAREER' UNION ALL SELECT 'MED', '2', 'GENSCI' UNION ALL SELECT 'MED', '2', 'KASAYSAYAN' UNION ALL SELECT 'CD1', '2', 'EFFCOMM' UNION ALL SELECT 'CD1', '2', 'MABKOM' UNION ALL SELECT 'CD1', '2', 'GENMATH' UNION ALL SELECT 'CD1', '2', 'LIFECAREER' UNION ALL SELECT 'CD1', '2', 'GENSCI' UNION ALL SELECT 'CD1', '2', 'KASAYSAYAN' UNION ALL SELECT 'HT1', '2', 'EFFCOMM' UNION ALL SELECT 'HT1', '2', 'MABKOM' UNION ALL SELECT 'HT1', '2', 'GENMATH' UNION ALL SELECT 'HT1', '2', 'LIFECAREER' UNION ALL SELECT 'HT1', '2', 'GENSCI' UNION ALL SELECT 'HT1', '2', 'KASAYSAYAN' UNION ALL SELECT 'HT2', '2', 'EFFCOMM' UNION ALL SELECT 'HT2', '2', 'MABKOM' UNION ALL SELECT 'HT2', '2', 'GENMATH' UNION ALL SELECT 'HT2', '2', 'LIFECAREER' UNION ALL SELECT 'HT2', '2', 'GENSCI' UNION ALL SELECT 'HT2', '2', 'KASAYSAYAN' UNION ALL SELECT 'USP', '2', 'EFFCOMM' UNION ALL SELECT 'USP', '2', 'MABKOM' UNION ALL SELECT 'USP', '2', 'GENMATH' UNION ALL SELECT 'USP', '2', 'LIFECAREER' UNION ALL SELECT 'USP', '2', 'GENSCI' UNION ALL SELECT 'USP', '2', 'KASAYSAYAN') d JOIN `strands` st ON st.`code` = d.`strand_code` JOIN `subjects` sub ON sub.`code` = d.`subject_code` WHERE NOT EXISTS (SELECT 1 FROM `strand_subjects` x WHERE x.`strand_id` = st.`id` AND x.`grade_level` = 11 AND x.`term_number` = d.`term_number` AND x.`subject_id` = sub.`id`);

UPDATE `enrollments` e JOIN `students` s ON s.`id` = e.`student_id` JOIN `sections` sec ON sec.`id` = e.`section_id` JOIN `strands` st ON st.`id` = sec.`strand_id` SET e.`lrn` = s.`lrn`, e.`first_name` = s.`first_name`, e.`middle_name` = s.`middle_name`, e.`last_name` = s.`last_name`, e.`grade_level` = sec.`grade_level`, e.`strand_id` = sec.`strand_id`, e.`track_id` = st.`track_id` WHERE e.`id` > 0;

ALTER TABLE `enrollments` MODIFY COLUMN `lrn` char(12) NOT NULL, MODIFY COLUMN `first_name` varchar(100) NOT NULL, MODIFY COLUMN `last_name` varchar(100) NOT NULL, MODIFY COLUMN `grade_level` tinyint unsigned NOT NULL, MODIFY COLUMN `track_id` int NOT NULL, MODIFY COLUMN `strand_id` int NOT NULL;

INSERT INTO `enrollment_subjects` (`enrollment_id`, `subject_id`) SELECT DISTINCT e.`id`, co.`subject_id` FROM `enrollments` e JOIN `class_offerings` co ON co.`term_id` = e.`term_id` AND co.`section_id` = e.`section_id` WHERE e.`status` = 'Enrolled';

UPDATE `enrollments` e SET e.`subjects` = (SELECT LEFT(GROUP_CONCAT(sub.`name` ORDER BY sub.`name` SEPARATOR ', '), 1000) FROM `enrollment_subjects` es JOIN `subjects` sub ON sub.`id` = es.`subject_id` WHERE es.`enrollment_id` = e.`id`) WHERE e.`id` > 0;

CREATE INDEX `ix_sections_schedule_id` ON `sections` (`schedule_id`);

CREATE UNIQUE INDEX `ix_sections_school_year_id_strand_id_grade_level_schedule_id_na` ON `sections` (`school_year_id`, `strand_id`, `grade_level`, `schedule_id`, `name`);

CREATE INDEX `ix_sections_strand_id_schedule_id` ON `sections` (`strand_id`, `schedule_id`);

ALTER TABLE `sections` DROP INDEX `ix_sections_school_year_id_strand_id_grade_level_name`;

ALTER TABLE `sections` DROP INDEX `ix_sections_strand_id`;

CREATE INDEX `ix_enrollments_section_id_strand_id_grade_level` ON `enrollments` (`section_id`, `strand_id`, `grade_level`);

CREATE INDEX `ix_enrollments_strand_id_track_id` ON `enrollments` (`strand_id`, `track_id`);

CREATE INDEX `ix_enrollments_track_id` ON `enrollments` (`track_id`);

ALTER TABLE `enrollments` ADD CONSTRAINT `ck_enrollments_grade_level` CHECK (grade_level IN (11, 12));

CREATE INDEX `ix_enrollment_subjects_subject_id` ON `enrollment_subjects` (`subject_id`);

CREATE UNIQUE INDEX `ix_schedules_code` ON `schedules` (`code`);

CREATE UNIQUE INDEX `ix_schedules_name` ON `schedules` (`name`);

CREATE INDEX `ix_strand_schedules_schedule_id` ON `strand_schedules` (`schedule_id`);

CREATE INDEX `ix_strand_subjects_subject_id` ON `strand_subjects` (`subject_id`);

ALTER TABLE `enrollments` ADD CONSTRAINT `fk_enrollments_sections_section_id_strand_id_grade_level` FOREIGN KEY (`section_id`, `strand_id`, `grade_level`) REFERENCES `sections` (`id`, `strand_id`, `grade_level`) ON DELETE RESTRICT;

ALTER TABLE `enrollments` ADD CONSTRAINT `fk_enrollments_strands_strand_id_track_id` FOREIGN KEY (`strand_id`, `track_id`) REFERENCES `strands` (`id`, `track_id`) ON DELETE RESTRICT;

ALTER TABLE `enrollments` ADD CONSTRAINT `fk_enrollments_tracks_track_id` FOREIGN KEY (`track_id`) REFERENCES `tracks` (`id`) ON DELETE RESTRICT;

ALTER TABLE `sections` ADD CONSTRAINT `fk_sections_schedules_schedule_id` FOREIGN KEY (`schedule_id`) REFERENCES `schedules` (`id`) ON DELETE RESTRICT;

ALTER TABLE `sections` ADD CONSTRAINT `fk_sections_strand_schedules_strand_id_schedule_id` FOREIGN KEY (`strand_id`, `schedule_id`) REFERENCES `strand_schedules` (`strand_id`, `schedule_id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261004120510_AddSchedulesCurriculumAndEnrollmentDetails', '9.0.20');

DROP TABLE `class_offerings`;

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261004141346_RemoveClassOfferings', '9.0.20');

SET FOREIGN_KEY_CHECKS = 0;

UPDATE `students` SET `track_id` = (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'ACAD') AS `x`) WHERE `id` > 0 AND `track_id` = (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'TECH-PRO') AS `x`) AND (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'ACAD') AS `x`) IS NOT NULL AND `strand_id` = (SELECT `id` FROM (SELECT `id` FROM `strands` WHERE `code` = 'USP') AS `x`);

UPDATE `teachers` SET `track_id` = (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'ACAD') AS `x`) WHERE `id` > 0 AND `track_id` = (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'TECH-PRO') AS `x`) AND (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'ACAD') AS `x`) IS NOT NULL AND `strand_id` = (SELECT `id` FROM (SELECT `id` FROM `strands` WHERE `code` = 'USP') AS `x`);

UPDATE `enrollments` SET `track_id` = (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'ACAD') AS `x`) WHERE `id` > 0 AND `track_id` = (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'TECH-PRO') AS `x`) AND (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'ACAD') AS `x`) IS NOT NULL AND `strand_id` = (SELECT `id` FROM (SELECT `id` FROM `strands` WHERE `code` = 'USP') AS `x`);

UPDATE `strands` SET `track_id` = (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'ACAD') AS `x`) WHERE `id` > 0 AND `track_id` = (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'TECH-PRO') AS `x`) AND (SELECT `id` FROM (SELECT `id` FROM `tracks` WHERE `code` = 'ACAD') AS `x`) IS NOT NULL AND `code` = 'USP';

SET FOREIGN_KEY_CHECKS = 1;

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261004170025_MoveUspToAcad', '9.0.20');

CREATE TABLE `messages` (
    `id` int NOT NULL AUTO_INCREMENT,
    `sender_user_id` int NOT NULL,
    `recipient_user_id` int NOT NULL,
    `body` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
    `read_at_utc` datetime(6) NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_messages` PRIMARY KEY (`id`),
    CONSTRAINT `fk_messages_users_recipient_user_id` FOREIGN KEY (`recipient_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_messages_users_sender_user_id` FOREIGN KEY (`sender_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `notifications` (
    `id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,
    `kind` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `body` varchar(500) CHARACTER SET utf8mb4 NULL,
    `link` varchar(300) CHARACTER SET utf8mb4 NULL,
    `is_read` tinyint(1) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_notifications` PRIMARY KEY (`id`),
    CONSTRAINT `fk_notifications_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `ix_messages_recipient_user_id_read_at_utc` ON `messages` (`recipient_user_id`, `read_at_utc`);

CREATE INDEX `ix_messages_sender_user_id_recipient_user_id_created_at_utc` ON `messages` (`sender_user_id`, `recipient_user_id`, `created_at_utc`);

CREATE INDEX `ix_notifications_user_id_is_read_created_at_utc` ON `notifications` (`user_id`, `is_read`, `created_at_utc`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261004171144_AddMessagesAndNotifications', '9.0.20');

CREATE TABLE `teaching_assignments` (
    `id` int NOT NULL AUTO_INCREMENT,
    `teacher_id` int NOT NULL,
    `section_id` int NOT NULL,
    `subject_id` int NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_teaching_assignments` PRIMARY KEY (`id`),
    CONSTRAINT `fk_teaching_assignments_sections_section_id` FOREIGN KEY (`section_id`) REFERENCES `sections` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_teaching_assignments_subjects_subject_id` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`) ON DELETE RESTRICT,
    CONSTRAINT `fk_teaching_assignments_teachers_teacher_id` FOREIGN KEY (`teacher_id`) REFERENCES `teachers` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `modules` (
    `id` int NOT NULL AUTO_INCREMENT,
    `assignment_id` int NOT NULL,
    `title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `body` text CHARACTER SET utf8mb4 NULL,
    `link_url` varchar(500) CHARACTER SET utf8mb4 NULL,
    `position` int NOT NULL,
    `visible_from_utc` datetime(6) NULL,
    `notified_at_utc` datetime(6) NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_modules` PRIMARY KEY (`id`),
    CONSTRAINT `fk_modules_teaching_assignments_assignment_id` FOREIGN KEY (`assignment_id`) REFERENCES `teaching_assignments` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `module_items` (
    `id` int NOT NULL AUTO_INCREMENT,
    `module_id` int NOT NULL,
    `kind` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `instructions` text CHARACTER SET utf8mb4 NULL,
    `max_score` decimal(6,2) NOT NULL,
    `due_at_utc` datetime(6) NULL,
    `visible_from_utc` datetime(6) NULL,
    `notified_at_utc` datetime(6) NULL,
    `due_reminder_sent_at_utc` datetime(6) NULL,
    `position` int NOT NULL,
    `time_limit_minutes` int NULL,
    `shuffle_questions` tinyint(1) NOT NULL,
    `shuffle_options` tinyint(1) NOT NULL,
    `anti_cheat` tinyint(1) NOT NULL,
    `max_violations` int NOT NULL,
    `show_result` tinyint(1) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_module_items` PRIMARY KEY (`id`),
    CONSTRAINT `ck_module_items_kind` CHECK (kind IN ('Activity', 'Quiz', 'TaskPerformance')),
    CONSTRAINT `ck_module_items_max_score` CHECK (max_score > 0),
    CONSTRAINT `fk_module_items_modules_module_id` FOREIGN KEY (`module_id`) REFERENCES `modules` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `module_views` (
    `module_id` int NOT NULL,
    `student_id` int NOT NULL,
    `first_opened_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_module_views` PRIMARY KEY (`module_id`, `student_id`),
    CONSTRAINT `fk_module_views_modules_module_id` FOREIGN KEY (`module_id`) REFERENCES `modules` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_module_views_students_student_id` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `quiz_questions` (
    `id` int NOT NULL AUTO_INCREMENT,
    `item_id` int NOT NULL,
    `position` int NOT NULL,
    `text` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `kind` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `options_text` text CHARACTER SET utf8mb4 NULL,
    `correct_answer` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `points` decimal(6,2) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_quiz_questions` PRIMARY KEY (`id`),
    CONSTRAINT `ck_quiz_questions_kind` CHECK (kind IN ('MultipleChoice', 'TrueFalse', 'ShortAnswer')),
    CONSTRAINT `fk_quiz_questions_module_items_item_id` FOREIGN KEY (`item_id`) REFERENCES `module_items` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `submissions` (
    `id` int NOT NULL AUTO_INCREMENT,
    `item_id` int NOT NULL,
    `student_id` int NOT NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `started_at_utc` datetime(6) NULL,
    `submitted_at_utc` datetime(6) NULL,
    `is_late` tinyint(1) NOT NULL,
    `answer_text` text CHARACTER SET utf8mb4 NULL,
    `link_url` varchar(500) CHARACTER SET utf8mb4 NULL,
    `answers_json` text CHARACTER SET utf8mb4 NULL,
    `score` decimal(6,2) NULL,
    `feedback` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `graded_at_utc` datetime(6) NULL,
    `violation_count` int NOT NULL,
    `violation_log` text CHARACTER SET utf8mb4 NULL,
    `is_flagged` tinyint(1) NOT NULL,
    `auto_submit_reason` varchar(20) CHARACTER SET utf8mb4 NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_submissions` PRIMARY KEY (`id`),
    CONSTRAINT `ck_submissions_score` CHECK (score IS NULL OR score >= 0),
    CONSTRAINT `ck_submissions_status` CHECK (status IN ('InProgress', 'Submitted', 'Graded')),
    CONSTRAINT `fk_submissions_module_items_item_id` FOREIGN KEY (`item_id`) REFERENCES `module_items` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_submissions_students_student_id` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `ix_module_items_due_at_utc` ON `module_items` (`due_at_utc`);

CREATE INDEX `ix_module_items_module_id_kind_position` ON `module_items` (`module_id`, `kind`, `position`);

CREATE INDEX `ix_module_views_student_id` ON `module_views` (`student_id`);

CREATE INDEX `ix_modules_assignment_id_position` ON `modules` (`assignment_id`, `position`);

CREATE INDEX `ix_modules_visible_from_utc` ON `modules` (`visible_from_utc`);

CREATE INDEX `ix_quiz_questions_item_id_position` ON `quiz_questions` (`item_id`, `position`);

CREATE UNIQUE INDEX `ix_submissions_item_id_student_id` ON `submissions` (`item_id`, `student_id`);

CREATE INDEX `ix_submissions_student_id_status` ON `submissions` (`student_id`, `status`);

CREATE UNIQUE INDEX `ix_teaching_assignments_section_id_subject_id` ON `teaching_assignments` (`section_id`, `subject_id`);

CREATE INDEX `ix_teaching_assignments_subject_id` ON `teaching_assignments` (`subject_id`);

CREATE INDEX `ix_teaching_assignments_teacher_id` ON `teaching_assignments` (`teacher_id`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261004174532_AddSubjectsModulesAndSubmissions', '9.0.20');

CREATE TABLE `todo_items` (
    `id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,
    `title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `due_on` date NULL,
    `is_done` tinyint(1) NOT NULL,
    `done_at_utc` datetime(6) NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_todo_items` PRIMARY KEY (`id`),
    CONSTRAINT `fk_todo_items_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `ix_todo_items_user_id_is_done_due_on` ON `todo_items` (`user_id`, `is_done`, `due_on`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261004184220_AddTodoItems', '9.0.20');

CREATE TABLE `attendance_records` (
    `id` int NOT NULL AUTO_INCREMENT,
    `assignment_id` int NOT NULL,
    `student_id` int NOT NULL,
    `attended_on` date NOT NULL,
    `status` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `note` varchar(200) CHARACTER SET utf8mb4 NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_attendance_records` PRIMARY KEY (`id`),
    CONSTRAINT `ck_attendance_records_status` CHECK (status IN ('Present', 'Late', 'Absent')),
    CONSTRAINT `fk_attendance_records_students_student_id` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_attendance_records_teaching_assignments_assignment_id` FOREIGN KEY (`assignment_id`) REFERENCES `teaching_assignments` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `ix_attendance_records_assignment_id_student_id_attended_on` ON `attendance_records` (`assignment_id`, `student_id`, `attended_on`);

CREATE INDEX `ix_attendance_records_student_id_attended_on` ON `attendance_records` (`student_id`, `attended_on`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261004192015_AddAttendanceRecords', '9.0.20');

ALTER TABLE `students` ADD `status` varchar(10) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Active';

ALTER TABLE `students` ADD `status_changed_at_utc` datetime(6) NULL;

ALTER TABLE `students` ADD `status_note` varchar(300) CHARACTER SET utf8mb4 NULL;

CREATE TABLE `referrals` (
    `id` int NOT NULL AUTO_INCREMENT,
    `student_id` int NOT NULL,
    `assignment_id` int NULL,
    `reason` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `detail` varchar(300) CHARACTER SET utf8mb4 NOT NULL,
    `metric` decimal(6,2) NULL,
    `status` varchar(12) CHARACTER SET utf8mb4 NOT NULL,
    `intervention` varchar(100) CHARACTER SET utf8mb4 NULL,
    `notes` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `outcome` varchar(12) CHARACTER SET utf8mb4 NULL,
    `resolved_at_utc` datetime(6) NULL,
    `handled_by_user_id` int NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_referrals` PRIMARY KEY (`id`),
    CONSTRAINT `ck_referrals_outcome` CHECK (outcome IS NULL OR outcome IN ('Improved', 'NoChange')),
    CONSTRAINT `ck_referrals_reason` CHECK (reason IN ('FailingGrades', 'AtRisk', 'LowAttendance')),
    CONSTRAINT `ck_referrals_status` CHECK (status IN ('Open', 'InProgress', 'Resolved')),
    CONSTRAINT `fk_referrals_students_student_id` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_referrals_teaching_assignments_assignment_id` FOREIGN KEY (`assignment_id`) REFERENCES `teaching_assignments` (`id`) ON DELETE SET NULL,
    CONSTRAINT `fk_referrals_users_handled_by_user_id` FOREIGN KEY (`handled_by_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `withdrawal_requests` (
    `id` int NOT NULL AUTO_INCREMENT,
    `student_id` int NOT NULL,
    `reason` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `status` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `decided_at_utc` datetime(6) NULL,
    `decided_by_user_id` int NULL,
    `decision_note` varchar(500) CHARACTER SET utf8mb4 NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_withdrawal_requests` PRIMARY KEY (`id`),
    CONSTRAINT `ck_withdrawal_requests_status` CHECK (status IN ('Pending', 'Approved', 'Declined', 'Cancelled')),
    CONSTRAINT `fk_withdrawal_requests_students_student_id` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_withdrawal_requests_users_decided_by_user_id` FOREIGN KEY (`decided_by_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE INDEX `ix_students_status` ON `students` (`status`);

ALTER TABLE `students` ADD CONSTRAINT `ck_students_status` CHECK (status IN ('Active', 'Graduated', 'Withdrawn'));

CREATE INDEX `ix_referrals_assignment_id` ON `referrals` (`assignment_id`);

CREATE INDEX `ix_referrals_handled_by_user_id` ON `referrals` (`handled_by_user_id`);

CREATE INDEX `ix_referrals_status_reason` ON `referrals` (`status`, `reason`);

CREATE INDEX `ix_referrals_student_id_status` ON `referrals` (`student_id`, `status`);

CREATE INDEX `ix_withdrawal_requests_decided_by_user_id` ON `withdrawal_requests` (`decided_by_user_id`);

CREATE INDEX `ix_withdrawal_requests_status_student_id` ON `withdrawal_requests` (`status`, `student_id`);

CREATE INDEX `ix_withdrawal_requests_student_id` ON `withdrawal_requests` (`student_id`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261004194551_AddReferralsStudentStatusAndWithdrawals', '9.0.20');

DROP TABLE `withdrawal_requests`;

ALTER TABLE `students` DROP CONSTRAINT `ck_students_status`;

UPDATE students SET status = 'Active', status_note = NULL WHERE id > 0 AND status = 'Withdrawn';

ALTER TABLE `students` ADD CONSTRAINT `ck_students_status` CHECK (status IN ('Active', 'Graduated'));

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261007020549_RemoveWithdrawals', '9.0.20');

CREATE TABLE `announcements` (
    `id` int NOT NULL AUTO_INCREMENT,
    `author_user_id` int NOT NULL,
    `title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `body` text CHARACTER SET utf8mb4 NOT NULL,
    `audience` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `section_id` int NULL,
    `expires_on` date NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_announcements` PRIMARY KEY (`id`),
    CONSTRAINT `ck_announcements_audience` CHECK (audience IN ('Everyone', 'Students', 'Teachers')),
    CONSTRAINT `fk_announcements_sections_section_id` FOREIGN KEY (`section_id`) REFERENCES `sections` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_announcements_users_author_user_id` FOREIGN KEY (`author_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `ix_announcements_audience_created_at_utc` ON `announcements` (`audience`, `created_at_utc`);

CREATE INDEX `ix_announcements_author_user_id_created_at_utc` ON `announcements` (`author_user_id`, `created_at_utc`);

CREATE INDEX `ix_announcements_section_id` ON `announcements` (`section_id`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261007022229_AddAnnouncements', '9.0.20');

UPDATE `strands` SET `name` = 'Business and Entrepreneurship' WHERE `id` > 0 AND `code` = 'BUSSENT' AND `name` = 'BUSSENT';

UPDATE `strands` SET `name` = 'Education' WHERE `id` > 0 AND `code` = 'EDUC' AND `name` = 'EDUC';

UPDATE `strands` SET `name` = 'Engineering' WHERE `id` > 0 AND `code` = 'ENG' AND `name` = 'ENG';

UPDATE `strands` SET `name` = 'Medical and Health Sciences' WHERE `id` > 0 AND `code` = 'MED' AND `name` = 'MED';

UPDATE `strands` SET `name` = 'University Studies Preparatory' WHERE `id` > 0 AND `code` = 'USP' AND `name` = 'USP';

UPDATE `strands` SET `name` = 'Computer System Servicing - Video Game Development' WHERE `id` > 0 AND `code` = 'CD1' AND `name` = 'CSS-VGD';

UPDATE `strands` SET `name` = 'Kitchen Operations - Food and Beverage Services' WHERE `id` > 0 AND `code` = 'HT1' AND `name` = 'KO-FBS';

UPDATE `strands` SET `name` = 'Food and Beverage Services - Kitchen Operations' WHERE `id` > 0 AND `code` = 'HT2' AND `name` = 'FBS-KO';

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261007051735_FillStrandFullNames', '9.0.20');

DROP TABLE `todo_items`;

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261007094238_RemoveTodoItems', '9.0.20');

CREATE TABLE `academic_coordinators` (
    `id` int NOT NULL AUTO_INCREMENT,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    `user_id` int NOT NULL,
    `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `middle_name` varchar(100) CHARACTER SET utf8mb4 NULL,
    `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `name_extension` varchar(10) CHARACTER SET utf8mb4 NULL,
    `employee_number` varchar(30) CHARACTER SET utf8mb4 NULL,
    `phone_number` varchar(20) CHARACTER SET utf8mb4 NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `pk_academic_coordinators` PRIMARY KEY (`id`),
    CONSTRAINT `ck_academic_coordinators_status` CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT `fk_academic_coordinators_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

INSERT INTO `roles` (`id`, `name`)
VALUES (6, 'AcademicCoordinator');

CREATE UNIQUE INDEX `ix_academic_coordinators_employee_number` ON `academic_coordinators` (`employee_number`);

CREATE UNIQUE INDEX `ix_academic_coordinators_user_id` ON `academic_coordinators` (`user_id`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261008091522_AddAcademicCoordinatorRole', '9.0.20');

ALTER TABLE `modules` ADD `is_syllabus` tinyint(1) NOT NULL DEFAULT FALSE;

CREATE TABLE `material_modules` (
    `id` int NOT NULL AUTO_INCREMENT,
    `subject_id` int NOT NULL,
    `is_syllabus` tinyint(1) NOT NULL,
    `title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `body` text CHARACTER SET utf8mb4 NULL,
    `link_url` varchar(500) CHARACTER SET utf8mb4 NULL,
    `position` int NOT NULL,
    `created_by_user_id` int NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_material_modules` PRIMARY KEY (`id`),
    CONSTRAINT `fk_material_modules_subjects_subject_id` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_material_modules_users_created_by_user_id` FOREIGN KEY (`created_by_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `teacher_evaluations` (
    `id` int NOT NULL AUTO_INCREMENT,
    `title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `instructions` varchar(2000) CHARACTER SET utf8mb4 NULL,
    `sent_at_utc` datetime(6) NULL,
    `due_at_utc` datetime(6) NULL,
    `term_id` int NULL,
    `created_by_user_id` int NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_teacher_evaluations` PRIMARY KEY (`id`),
    CONSTRAINT `fk_teacher_evaluations_terms_term_id` FOREIGN KEY (`term_id`) REFERENCES `terms` (`id`) ON DELETE SET NULL,
    CONSTRAINT `fk_teacher_evaluations_users_created_by_user_id` FOREIGN KEY (`created_by_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `material_deliveries` (
    `id` int NOT NULL AUTO_INCREMENT,
    `material_module_id` int NOT NULL,
    `assignment_id` int NOT NULL,
    `module_id` int NOT NULL,
    `sent_by_user_id` int NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_material_deliveries` PRIMARY KEY (`id`),
    CONSTRAINT `fk_material_deliveries_material_modules_material_module_id` FOREIGN KEY (`material_module_id`) REFERENCES `material_modules` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_material_deliveries_modules_module_id` FOREIGN KEY (`module_id`) REFERENCES `modules` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_material_deliveries_teaching_assignments_assignment_id` FOREIGN KEY (`assignment_id`) REFERENCES `teaching_assignments` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_material_deliveries_users_sent_by_user_id` FOREIGN KEY (`sent_by_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `material_items` (
    `id` int NOT NULL AUTO_INCREMENT,
    `module_id` int NOT NULL,
    `kind` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
    `instructions` text CHARACTER SET utf8mb4 NULL,
    `max_score` decimal(6,2) NOT NULL,
    `due_at_utc` datetime(6) NULL,
    `position` int NOT NULL,
    `time_limit_minutes` int NULL,
    `shuffle_questions` tinyint(1) NOT NULL,
    `shuffle_options` tinyint(1) NOT NULL,
    `anti_cheat` tinyint(1) NOT NULL,
    `max_violations` int NOT NULL,
    `show_result` tinyint(1) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_material_items` PRIMARY KEY (`id`),
    CONSTRAINT `ck_material_items_kind` CHECK (kind IN ('Activity', 'Quiz', 'TaskPerformance')),
    CONSTRAINT `ck_material_items_max_score` CHECK (max_score > 0),
    CONSTRAINT `fk_material_items_material_modules_module_id` FOREIGN KEY (`module_id`) REFERENCES `material_modules` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `evaluation_questions` (
    `id` int NOT NULL AUTO_INCREMENT,
    `evaluation_id` int NOT NULL,
    `position` int NOT NULL,
    `text` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `kind` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `category` varchar(60) CHARACTER SET utf8mb4 NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_evaluation_questions` PRIMARY KEY (`id`),
    CONSTRAINT `ck_evaluation_questions_kind` CHECK (kind IN ('Rating', 'Comment')),
    CONSTRAINT `fk_evaluation_questions_teacher_evaluations_evaluation_id` FOREIGN KEY (`evaluation_id`) REFERENCES `teacher_evaluations` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `evaluation_responses` (
    `id` int NOT NULL AUTO_INCREMENT,
    `evaluation_id` int NOT NULL,
    `assignment_id` int NOT NULL,
    `student_id` int NOT NULL,
    `submitted_at_utc` datetime(6) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_evaluation_responses` PRIMARY KEY (`id`),
    CONSTRAINT `fk_evaluation_responses_students_student_id` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_evaluation_responses_teacher_evaluations_evaluation_id` FOREIGN KEY (`evaluation_id`) REFERENCES `teacher_evaluations` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_evaluation_responses_teaching_assignments_assignment_id` FOREIGN KEY (`assignment_id`) REFERENCES `teaching_assignments` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `evaluation_targets` (
    `evaluation_id` int NOT NULL,
    `assignment_id` int NOT NULL,
    CONSTRAINT `pk_evaluation_targets` PRIMARY KEY (`evaluation_id`, `assignment_id`),
    CONSTRAINT `fk_evaluation_targets_teacher_evaluations_evaluation_id` FOREIGN KEY (`evaluation_id`) REFERENCES `teacher_evaluations` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_evaluation_targets_teaching_assignments_assignment_id` FOREIGN KEY (`assignment_id`) REFERENCES `teaching_assignments` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `material_questions` (
    `id` int NOT NULL AUTO_INCREMENT,
    `item_id` int NOT NULL,
    `position` int NOT NULL,
    `text` varchar(1000) CHARACTER SET utf8mb4 NOT NULL,
    `kind` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `options_text` text CHARACTER SET utf8mb4 NULL,
    `correct_answer` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
    `points` decimal(6,2) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_material_questions` PRIMARY KEY (`id`),
    CONSTRAINT `ck_material_questions_kind` CHECK (kind IN ('MultipleChoice', 'TrueFalse', 'ShortAnswer')),
    CONSTRAINT `fk_material_questions_material_items_item_id` FOREIGN KEY (`item_id`) REFERENCES `material_items` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `evaluation_answers` (
    `response_id` int NOT NULL,
    `question_id` int NOT NULL,
    `rating` tinyint unsigned NULL,
    `comment` varchar(1000) CHARACTER SET utf8mb4 NULL,
    CONSTRAINT `pk_evaluation_answers` PRIMARY KEY (`response_id`, `question_id`),
    CONSTRAINT `ck_evaluation_answers_rating` CHECK (rating IS NULL OR rating BETWEEN 1 AND 5),
    CONSTRAINT `fk_evaluation_answers_evaluation_questions_question_id` FOREIGN KEY (`question_id`) REFERENCES `evaluation_questions` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_evaluation_answers_evaluation_responses_response_id` FOREIGN KEY (`response_id`) REFERENCES `evaluation_responses` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `ix_evaluation_answers_question_id` ON `evaluation_answers` (`question_id`);

CREATE INDEX `ix_evaluation_questions_evaluation_id_position` ON `evaluation_questions` (`evaluation_id`, `position`);

CREATE INDEX `ix_evaluation_responses_assignment_id` ON `evaluation_responses` (`assignment_id`);

CREATE UNIQUE INDEX `ix_evaluation_responses_evaluation_id_assignment_id_student_id` ON `evaluation_responses` (`evaluation_id`, `assignment_id`, `student_id`);

CREATE INDEX `ix_evaluation_responses_student_id_evaluation_id` ON `evaluation_responses` (`student_id`, `evaluation_id`);

CREATE INDEX `ix_evaluation_targets_assignment_id` ON `evaluation_targets` (`assignment_id`);

CREATE INDEX `ix_material_deliveries_assignment_id` ON `material_deliveries` (`assignment_id`);

CREATE UNIQUE INDEX `ix_material_deliveries_material_module_id_assignment_id` ON `material_deliveries` (`material_module_id`, `assignment_id`);

CREATE UNIQUE INDEX `ix_material_deliveries_module_id` ON `material_deliveries` (`module_id`);

CREATE INDEX `ix_material_deliveries_sent_by_user_id` ON `material_deliveries` (`sent_by_user_id`);

CREATE INDEX `ix_material_items_module_id_position` ON `material_items` (`module_id`, `position`);

CREATE INDEX `ix_material_modules_created_by_user_id` ON `material_modules` (`created_by_user_id`);

CREATE INDEX `ix_material_modules_subject_id_position` ON `material_modules` (`subject_id`, `position`);

CREATE INDEX `ix_material_questions_item_id_position` ON `material_questions` (`item_id`, `position`);

CREATE INDEX `ix_teacher_evaluations_created_by_user_id` ON `teacher_evaluations` (`created_by_user_id`);

CREATE INDEX `ix_teacher_evaluations_sent_at_utc` ON `teacher_evaluations` (`sent_at_utc`);

CREATE INDEX `ix_teacher_evaluations_term_id` ON `teacher_evaluations` (`term_id`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261008092129_AddCourseMaterialsAndTeacherEvaluation', '9.0.20');

INSERT INTO `sections` (`school_year_id`, `strand_id`, `grade_level`, `schedule_id`, `name`, `created_at_utc`, `updated_at_utc`) SELECT y.`id`, ss.`strand_id`, c.`grade_level`, ss.`schedule_id`, n.`name`, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM (SELECT COALESCE((SELECT `school_year_id` FROM `terms` WHERE `is_current` = 1 ORDER BY `id` LIMIT 1),                       (SELECT `id` FROM `school_years` ORDER BY `start_date` DESC, `id` DESC LIMIT 1)) AS `id`) y JOIN `strand_schedules` ss JOIN (SELECT DISTINCT `strand_id`, `grade_level` FROM `strand_subjects`) c ON c.`strand_id` = ss.`strand_id` JOIN (SELECT 'A' AS `name` UNION ALL SELECT 'B' UNION ALL SELECT 'C' UNION ALL SELECT 'D' UNION ALL SELECT 'E') n WHERE y.`id` IS NOT NULL   AND NOT EXISTS (SELECT 1 FROM `sections` x WHERE x.`school_year_id` = y.`id` AND x.`strand_id` = ss.`strand_id`                   AND x.`grade_level` = c.`grade_level` AND x.`schedule_id` = ss.`schedule_id` AND x.`name` = n.`name`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261008092155_AddSectionsAToE', '9.0.20');

CREATE TABLE `profile_photos` (
    `user_id` int NOT NULL,
    `content_type` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `data` mediumblob NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_profile_photos` PRIMARY KEY (`user_id`),
    CONSTRAINT `fk_profile_photos_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261012031156_AddProfilePhotos', '9.0.20');

CREATE TABLE `friendships` (
    `id` int NOT NULL AUTO_INCREMENT,
    `requester_user_id` int NOT NULL,
    `addressee_user_id` int NOT NULL,
    `status` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `responded_at_utc` datetime(6) NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_friendships` PRIMARY KEY (`id`),
    CONSTRAINT `ck_friendships_status` CHECK (status IN ('Pending', 'Accepted')),
    CONSTRAINT `fk_friendships_users_addressee_user_id` FOREIGN KEY (`addressee_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_friendships_users_requester_user_id` FOREIGN KEY (`requester_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `security_logs` (
    `id` int NOT NULL AUTO_INCREMENT,
    `kind` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
    `severity` varchar(10) CHARACTER SET utf8mb4 NOT NULL,
    `user_id` int NULL,
    `email` varchar(254) CHARACTER SET utf8mb4 NULL,
    `actor_user_id` int NULL,
    `actor_name` varchar(200) CHARACTER SET utf8mb4 NULL,
    `ip_address` varchar(45) CHARACTER SET utf8mb4 NULL,
    `user_agent` varchar(300) CHARACTER SET utf8mb4 NULL,
    `details` varchar(1000) CHARACTER SET utf8mb4 NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_security_logs` PRIMARY KEY (`id`),
    CONSTRAINT `fk_security_logs_users_actor_user_id` FOREIGN KEY (`actor_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL,
    CONSTRAINT `fk_security_logs_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `stored_files` (
    `id` int NOT NULL AUTO_INCREMENT,
    `owner_user_id` int NOT NULL,
    `name` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `content_type` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `size_bytes` bigint NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_stored_files` PRIMARY KEY (`id`),
    CONSTRAINT `ck_stored_files_size` CHECK (size_bytes >= 0),
    CONSTRAINT `fk_stored_files_users_owner_user_id` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `study_groups` (
    `id` int NOT NULL AUTO_INCREMENT,
    `name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `description` varchar(500) CHARACTER SET utf8mb4 NULL,
    `owner_user_id` int NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_study_groups` PRIMARY KEY (`id`),
    CONSTRAINT `fk_study_groups_users_owner_user_id` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `system_settings` (
    `key` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
    `value` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    `updated_by_user_id` int NULL,
    CONSTRAINT `pk_system_settings` PRIMARY KEY (`key`),
    CONSTRAINT `fk_system_settings_users_updated_by_user_id` FOREIGN KEY (`updated_by_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `stored_file_contents` (
    `file_id` int NOT NULL,
    `data` longblob NOT NULL,
    CONSTRAINT `pk_stored_file_contents` PRIMARY KEY (`file_id`),
    CONSTRAINT `fk_stored_file_contents_stored_files_file_id` FOREIGN KEY (`file_id`) REFERENCES `stored_files` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `group_posts` (
    `id` int NOT NULL AUTO_INCREMENT,
    `group_id` int NOT NULL,
    `author_user_id` int NOT NULL,
    `body` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
    `parent_post_id` int NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_group_posts` PRIMARY KEY (`id`),
    CONSTRAINT `fk_group_posts_group_posts_parent_post_id` FOREIGN KEY (`parent_post_id`) REFERENCES `group_posts` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_group_posts_study_groups_group_id` FOREIGN KEY (`group_id`) REFERENCES `study_groups` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_group_posts_users_author_user_id` FOREIGN KEY (`author_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `shared_files` (
    `id` int NOT NULL AUTO_INCREMENT,
    `file_id` int NOT NULL,
    `shared_with_user_id` int NULL,
    `group_id` int NULL,
    `assignment_id` int NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_shared_files` PRIMARY KEY (`id`),
    CONSTRAINT `fk_shared_files_stored_files_file_id` FOREIGN KEY (`file_id`) REFERENCES `stored_files` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_shared_files_study_groups_group_id` FOREIGN KEY (`group_id`) REFERENCES `study_groups` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_shared_files_teaching_assignments_assignment_id` FOREIGN KEY (`assignment_id`) REFERENCES `teaching_assignments` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_shared_files_users_shared_with_user_id` FOREIGN KEY (`shared_with_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `study_group_members` (
    `group_id` int NOT NULL,
    `user_id` int NOT NULL,
    `joined_at_utc` datetime(6) NOT NULL,
    `added_by_user_id` int NULL,
    CONSTRAINT `pk_study_group_members` PRIMARY KEY (`group_id`, `user_id`),
    CONSTRAINT `fk_study_group_members_study_groups_group_id` FOREIGN KEY (`group_id`) REFERENCES `study_groups` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_study_group_members_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `ix_friendships_addressee_user_id_status` ON `friendships` (`addressee_user_id`, `status`);

CREATE UNIQUE INDEX `ix_friendships_requester_user_id_addressee_user_id` ON `friendships` (`requester_user_id`, `addressee_user_id`);

CREATE INDEX `ix_group_posts_author_user_id` ON `group_posts` (`author_user_id`);

CREATE INDEX `ix_group_posts_group_id_created_at_utc` ON `group_posts` (`group_id`, `created_at_utc`);

CREATE INDEX `ix_group_posts_parent_post_id` ON `group_posts` (`parent_post_id`);

CREATE INDEX `ix_security_logs_actor_user_id` ON `security_logs` (`actor_user_id`);

CREATE INDEX `ix_security_logs_created_at_utc` ON `security_logs` (`created_at_utc`);

CREATE INDEX `ix_security_logs_kind_created_at_utc` ON `security_logs` (`kind`, `created_at_utc`);

CREATE INDEX `ix_security_logs_user_id` ON `security_logs` (`user_id`);

CREATE INDEX `ix_shared_files_assignment_id` ON `shared_files` (`assignment_id`);

CREATE UNIQUE INDEX `ix_shared_files_file_id_assignment_id` ON `shared_files` (`file_id`, `assignment_id`);

CREATE UNIQUE INDEX `ix_shared_files_file_id_group_id` ON `shared_files` (`file_id`, `group_id`);

CREATE UNIQUE INDEX `ix_shared_files_file_id_shared_with_user_id` ON `shared_files` (`file_id`, `shared_with_user_id`);

CREATE INDEX `ix_shared_files_group_id` ON `shared_files` (`group_id`);

CREATE INDEX `ix_shared_files_shared_with_user_id` ON `shared_files` (`shared_with_user_id`);

CREATE INDEX `ix_stored_files_owner_user_id_created_at_utc` ON `stored_files` (`owner_user_id`, `created_at_utc`);

CREATE INDEX `ix_study_group_members_user_id` ON `study_group_members` (`user_id`);

CREATE INDEX `ix_study_groups_owner_user_id` ON `study_groups` (`owner_user_id`);

CREATE INDEX `ix_system_settings_updated_by_user_id` ON `system_settings` (`updated_by_user_id`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261012031512_AddSocialFeaturesSecurityLogsAndSettings', '9.0.20');

ALTER TABLE `material_deliveries` ADD `opened_at_utc` datetime(6) NULL;

CREATE TABLE `academic_providers` (
    `id` int NOT NULL AUTO_INCREMENT,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    `user_id` int NOT NULL,
    `first_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `middle_name` varchar(100) CHARACTER SET utf8mb4 NULL,
    `last_name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `name_extension` varchar(10) CHARACTER SET utf8mb4 NULL,
    `employee_number` varchar(30) CHARACTER SET utf8mb4 NULL,
    `phone_number` varchar(20) CHARACTER SET utf8mb4 NULL,
    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `pk_academic_providers` PRIMARY KEY (`id`),
    CONSTRAINT `ck_academic_providers_status` CHECK (status IN ('Active', 'Inactive')),
    CONSTRAINT `fk_academic_providers_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `item_views` (
    `item_id` int NOT NULL,
    `student_id` int NOT NULL,
    `first_opened_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_item_views` PRIMARY KEY (`item_id`, `student_id`),
    CONSTRAINT `fk_item_views_module_items_item_id` FOREIGN KEY (`item_id`) REFERENCES `module_items` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_item_views_students_student_id` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

INSERT INTO `roles` (`id`, `name`)
VALUES (7, 'AcademicProvider');

CREATE UNIQUE INDEX `ix_academic_providers_employee_number` ON `academic_providers` (`employee_number`);

CREATE UNIQUE INDEX `ix_academic_providers_user_id` ON `academic_providers` (`user_id`);

CREATE INDEX `ix_item_views_student_id` ON `item_views` (`student_id`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261012045434_AddAcademicProviderRoleAndOpenTracking', '9.0.20');

INSERT INTO `subjects` (`code`, `name`, `is_active`, `created_at_utc`, `updated_at_utc`) SELECT d.`code`, d.`name`, 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM (SELECT 'BUSACCT2' AS `code`, 'BUSINESS ACCOUNTING 2' AS `name` UNION ALL SELECT 'ADVMATH2', 'ADVANCE MATHEMATICS 2' UNION ALL SELECT 'FIL2', 'FILIPINO 2' UNION ALL SELECT 'CREACOMP2', 'CREATIVE COMPOSITION 2' UNION ALL SELECT 'FINMATH2', 'FINITE MATH 2' UNION ALL SELECT 'EMPOWERTECH2', 'EMPOWERMENT TECHNOLOGY 2' UNION ALL SELECT 'BIO2', 'BIOLOGY 2' UNION ALL SELECT 'SOCSCI2', 'SOCIAL SCIENCES 2' UNION ALL SELECT 'CSS2', 'COMPUTER SYSTEM SERVICING 2' UNION ALL SELECT 'KITCHENOPS2', 'KITCHEN OPERATION 2' UNION ALL SELECT 'FBSOPS2', 'FOOD AND BEVERAGE OPERATIONS 2' UNION ALL SELECT 'EFFCOMM2', 'EFFECTIVE COMMUNICATION 2' UNION ALL SELECT 'MABKOM2', 'MABISANG KOMUNIKASYON 2' UNION ALL SELECT 'GENMATH2', 'GENERAL MATHEMATICS 2' UNION ALL SELECT 'LIFECAREER2', 'LIFE AND CAREER SKILLS 2' UNION ALL SELECT 'GENSCI2', 'GENERAL SCIENCE 2' UNION ALL SELECT 'KASAYSAYAN2', 'PAG-AARAL NG KASAYSAYAN AT LIPUNANG PILIPINO 2') d WHERE NOT EXISTS (SELECT 1 FROM `subjects` s WHERE s.`code` = d.`code`);

INSERT INTO `strand_subjects` (`strand_id`, `grade_level`, `term_number`, `subject_id`) SELECT st.`id`, 12, d.`term_number`, sub.`id` FROM (SELECT 'BUSSENT' AS `strand_code`, '1' AS `term_number`, 'BUSACCT2' AS `subject_code` UNION ALL SELECT 'BUSSENT', '1', 'ADVMATH2' UNION ALL SELECT 'EDUC', '1', 'FIL2' UNION ALL SELECT 'EDUC', '1', 'CREACOMP2' UNION ALL SELECT 'ENG', '1', 'FINMATH2' UNION ALL SELECT 'ENG', '1', 'EMPOWERTECH2' UNION ALL SELECT 'MED', '1', 'BIO2' UNION ALL SELECT 'MED', '1', 'EMPOWERTECH2' UNION ALL SELECT 'USP', '1', 'SOCSCI2' UNION ALL SELECT 'USP', '1', 'BIO2' UNION ALL SELECT 'CD1', '1', 'CSS2' UNION ALL SELECT 'HT1', '1', 'KITCHENOPS2' UNION ALL SELECT 'HT2', '1', 'FBSOPS2' UNION ALL SELECT 'BUSSENT', '2', 'EFFCOMM2' UNION ALL SELECT 'BUSSENT', '2', 'MABKOM2' UNION ALL SELECT 'BUSSENT', '2', 'GENMATH2' UNION ALL SELECT 'BUSSENT', '2', 'LIFECAREER2' UNION ALL SELECT 'BUSSENT', '2', 'GENSCI2' UNION ALL SELECT 'BUSSENT', '2', 'KASAYSAYAN2' UNION ALL SELECT 'EDUC', '2', 'EFFCOMM2' UNION ALL SELECT 'EDUC', '2', 'MABKOM2' UNION ALL SELECT 'EDUC', '2', 'GENMATH2' UNION ALL SELECT 'EDUC', '2', 'LIFECAREER2' UNION ALL SELECT 'EDUC', '2', 'GENSCI2' UNION ALL SELECT 'EDUC', '2', 'KASAYSAYAN2' UNION ALL SELECT 'ENG', '2', 'EFFCOMM2' UNION ALL SELECT 'ENG', '2', 'MABKOM2' UNION ALL SELECT 'ENG', '2', 'GENMATH2' UNION ALL SELECT 'ENG', '2', 'LIFECAREER2' UNION ALL SELECT 'ENG', '2', 'GENSCI2' UNION ALL SELECT 'ENG', '2', 'KASAYSAYAN2' UNION ALL SELECT 'MED', '2', 'EFFCOMM2' UNION ALL SELECT 'MED', '2', 'MABKOM2' UNION ALL SELECT 'MED', '2', 'GENMATH2' UNION ALL SELECT 'MED', '2', 'LIFECAREER2' UNION ALL SELECT 'MED', '2', 'GENSCI2' UNION ALL SELECT 'MED', '2', 'KASAYSAYAN2' UNION ALL SELECT 'USP', '2', 'EFFCOMM2' UNION ALL SELECT 'USP', '2', 'MABKOM2' UNION ALL SELECT 'USP', '2', 'GENMATH2' UNION ALL SELECT 'USP', '2', 'LIFECAREER2' UNION ALL SELECT 'USP', '2', 'GENSCI2' UNION ALL SELECT 'USP', '2', 'KASAYSAYAN2' UNION ALL SELECT 'CD1', '2', 'EFFCOMM2' UNION ALL SELECT 'CD1', '2', 'MABKOM2' UNION ALL SELECT 'CD1', '2', 'GENMATH2' UNION ALL SELECT 'CD1', '2', 'LIFECAREER2' UNION ALL SELECT 'CD1', '2', 'GENSCI2' UNION ALL SELECT 'CD1', '2', 'KASAYSAYAN2' UNION ALL SELECT 'HT1', '2', 'EFFCOMM2' UNION ALL SELECT 'HT1', '2', 'MABKOM2' UNION ALL SELECT 'HT1', '2', 'GENMATH2' UNION ALL SELECT 'HT1', '2', 'LIFECAREER2' UNION ALL SELECT 'HT1', '2', 'GENSCI2' UNION ALL SELECT 'HT1', '2', 'KASAYSAYAN2' UNION ALL SELECT 'HT2', '2', 'EFFCOMM2' UNION ALL SELECT 'HT2', '2', 'MABKOM2' UNION ALL SELECT 'HT2', '2', 'GENMATH2' UNION ALL SELECT 'HT2', '2', 'LIFECAREER2' UNION ALL SELECT 'HT2', '2', 'GENSCI2' UNION ALL SELECT 'HT2', '2', 'KASAYSAYAN2') d JOIN `strands` st ON st.`code` = d.`strand_code` JOIN `subjects` sub ON sub.`code` = d.`subject_code` WHERE NOT EXISTS (SELECT 1 FROM `strand_subjects` x WHERE x.`strand_id` = st.`id` AND x.`grade_level` = 12 AND x.`term_number` = d.`term_number` AND x.`subject_id` = sub.`id`);

INSERT INTO `sections` (`school_year_id`, `strand_id`, `grade_level`, `schedule_id`, `name`, `created_at_utc`, `updated_at_utc`) SELECT y.`id`, ss.`strand_id`, c.`grade_level`, ss.`schedule_id`, n.`name`, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) FROM (SELECT COALESCE((SELECT `school_year_id` FROM `terms` WHERE `is_current` = 1 ORDER BY `id` LIMIT 1),                       (SELECT `id` FROM `school_years` ORDER BY `start_date` DESC, `id` DESC LIMIT 1)) AS `id`) y JOIN `strand_schedules` ss JOIN (SELECT DISTINCT `strand_id`, `grade_level` FROM `strand_subjects`) c ON c.`strand_id` = ss.`strand_id` JOIN (SELECT 'A' AS `name` UNION ALL SELECT 'B' UNION ALL SELECT 'C') n WHERE y.`id` IS NOT NULL   AND NOT EXISTS (SELECT 1 FROM `sections` x WHERE x.`school_year_id` = y.`id` AND x.`strand_id` = ss.`strand_id`                   AND x.`grade_level` = c.`grade_level` AND x.`schedule_id` = ss.`schedule_id` AND x.`name` = n.`name`);

DELETE FROM `sections` WHERE `id` > 0 AND `name` IN ('D', 'E') AND NOT EXISTS (SELECT 1 FROM `enrollments` e WHERE e.`section_id` = `sections`.`id`) AND NOT EXISTS (SELECT 1 FROM `teaching_assignments` a WHERE a.`section_id` = `sections`.`id`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261012144652_AddGrade12CurriculumAndSectionsAToC', '9.0.20');

ALTER TABLE `study_groups` MODIFY COLUMN `owner_user_id` int NULL;

ALTER TABLE `study_groups` ADD `auto_key` int NULL;

ALTER TABLE `study_groups` ADD `auto_kind` varchar(20) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `announcements` ADD `assignment_id` int NULL;

ALTER TABLE `announcements` ADD `category` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Other';

CREATE UNIQUE INDEX `ix_study_groups_auto_kind_auto_key` ON `study_groups` (`auto_kind`, `auto_key`);

ALTER TABLE `study_groups` ADD CONSTRAINT `ck_study_groups_auto_kind` CHECK (auto_kind IS NULL OR auto_kind IN ('GradeStudents', 'CourseStudents', 'AllStudents', 'GradeTeachers', 'AllTeachers'));

CREATE INDEX `ix_announcements_assignment_id` ON `announcements` (`assignment_id`);

ALTER TABLE `announcements` ADD CONSTRAINT `ck_announcements_category` CHECK (category IN ('Exam', 'Holiday', 'SemesterBreak', 'TeachersMeeting', 'Other'));

ALTER TABLE `announcements` ADD CONSTRAINT `fk_announcements_teaching_assignments_assignment_id` FOREIGN KEY (`assignment_id`) REFERENCES `teaching_assignments` (`id`) ON DELETE CASCADE;

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261012145934_AddAnnouncementCategoriesAndAutomaticGroups', '9.0.20');

ALTER TABLE `evaluation_questions` DROP CONSTRAINT `ck_evaluation_questions_kind`;

ALTER TABLE `evaluation_questions` MODIFY COLUMN `kind` varchar(12) CHARACTER SET utf8mb4 NOT NULL;

ALTER TABLE `evaluation_questions` ADD `options_text` text CHARACTER SET utf8mb4 NULL;

ALTER TABLE `evaluation_answers` ADD `selection` varchar(100) CHARACTER SET utf8mb4 NULL;

UPDATE `evaluation_questions` SET `kind` = 'Performance' WHERE `id` > 0 AND `kind` = 'Rating';

ALTER TABLE `evaluation_questions` ADD CONSTRAINT `ck_evaluation_questions_kind` CHECK (kind IN ('Likert', 'Performance', 'Frequency', 'Binary', 'Ranking', 'SelectAll', 'Comment'));

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261012151328_AddEvaluationScales', '9.0.20');

CREATE TABLE `attachments` (
    `id` int NOT NULL AUTO_INCREMENT,
    `name` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `content_type` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `size_bytes` bigint NOT NULL,
    `uploaded_by_user_id` int NULL,
    `material_module_id` int NULL,
    `material_item_id` int NULL,
    `module_id` int NULL,
    `item_id` int NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_attachments` PRIMARY KEY (`id`),
    CONSTRAINT `ck_attachments_size` CHECK (size_bytes >= 0),
    CONSTRAINT `fk_attachments_material_items_material_item_id` FOREIGN KEY (`material_item_id`) REFERENCES `material_items` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_attachments_material_modules_material_module_id` FOREIGN KEY (`material_module_id`) REFERENCES `material_modules` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_attachments_module_items_item_id` FOREIGN KEY (`item_id`) REFERENCES `module_items` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_attachments_modules_module_id` FOREIGN KEY (`module_id`) REFERENCES `modules` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_attachments_users_uploaded_by_user_id` FOREIGN KEY (`uploaded_by_user_id`) REFERENCES `users` (`id`) ON DELETE SET NULL
) CHARACTER SET=utf8mb4;

CREATE TABLE `attachment_contents` (
    `attachment_id` int NOT NULL,
    `data` longblob NOT NULL,
    CONSTRAINT `pk_attachment_contents` PRIMARY KEY (`attachment_id`),
    CONSTRAINT `fk_attachment_contents_attachments_attachment_id` FOREIGN KEY (`attachment_id`) REFERENCES `attachments` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `ix_attachments_item_id` ON `attachments` (`item_id`);

CREATE INDEX `ix_attachments_material_item_id` ON `attachments` (`material_item_id`);

CREATE INDEX `ix_attachments_material_module_id` ON `attachments` (`material_module_id`);

CREATE INDEX `ix_attachments_module_id` ON `attachments` (`module_id`);

CREATE INDEX `ix_attachments_uploaded_by_user_id` ON `attachments` (`uploaded_by_user_id`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261012151904_AddAttachments', '9.0.20');

ALTER TABLE `submissions` ADD `answer_html` mediumtext CHARACTER SET utf8mb4 NULL;

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261013025153_AddRichAnswers', '9.0.20');

CREATE TABLE `achievement_awards` (
    `id` int NOT NULL AUTO_INCREMENT,
    `student_id` int NOT NULL,
    `achievement_key` varchar(40) CHARACTER SET utf8mb4 NOT NULL,
    `awarded_at_utc` datetime(6) NOT NULL,
    `seen_at_utc` datetime(6) NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_achievement_awards` PRIMARY KEY (`id`),
    CONSTRAINT `fk_achievement_awards_students_student_id` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE TABLE `calendar_entries` (
    `id` int NOT NULL AUTO_INCREMENT,
    `user_id` int NOT NULL,
    `title` varchar(120) CHARACTER SET utf8mb4 NOT NULL,
    `day` date NOT NULL,
    `start_time` time(6) NULL,
    `notes` varchar(500) CHARACTER SET utf8mb4 NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_calendar_entries` PRIMARY KEY (`id`),
    CONSTRAINT `fk_calendar_entries_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE UNIQUE INDEX `ix_achievement_awards_student_id_achievement_key` ON `achievement_awards` (`student_id`, `achievement_key`);

CREATE INDEX `ix_calendar_entries_user_id_day` ON `calendar_entries` (`user_id`, `day`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261013065349_AddCalendarEntriesAndAwards', '9.0.20');

ALTER TABLE `schedules` ADD `end_time` time(6) NOT NULL DEFAULT '00:00:00';

ALTER TABLE `schedules` ADD `start_time` time(6) NOT NULL DEFAULT '00:00:00';

CREATE TABLE `course_schedules` (
    `id` int NOT NULL AUTO_INCREMENT,
    `section_id` int NOT NULL,
    `subject_id` int NOT NULL,
    `days` varchar(3) CHARACTER SET utf8mb4 NOT NULL,
    `start_time` time(6) NOT NULL,
    `end_time` time(6) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_course_schedules` PRIMARY KEY (`id`),
    CONSTRAINT `ck_course_schedules_days` CHECK (days IN ('MWF', 'TTH')),
    CONSTRAINT `ck_course_schedules_hours` CHECK (end_time > start_time),
    CONSTRAINT `fk_course_schedules_sections_section_id` FOREIGN KEY (`section_id`) REFERENCES `sections` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_course_schedules_subjects_subject_id` FOREIGN KEY (`subject_id`) REFERENCES `subjects` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

UPDATE `schedules` SET `start_time` = '07:00:00', `end_time` = '12:00:00' WHERE `code` = 'AM' AND `id` > 0;

UPDATE `schedules` SET `start_time` = '10:00:00', `end_time` = '15:00:00' WHERE `code` = 'MID' AND `id` > 0;

UPDATE `schedules` SET `start_time` = '13:00:00', `end_time` = '18:00:00' WHERE `code` = 'PM' AND `id` > 0;

UPDATE `schedules` SET `start_time` = '07:00:00', `end_time` = '17:00:00' WHERE `start_time` = `end_time` AND `id` > 0;

ALTER TABLE `schedules` MODIFY COLUMN `end_time` time(6) NOT NULL;

ALTER TABLE `schedules` MODIFY COLUMN `start_time` time(6) NOT NULL;

ALTER TABLE `schedules` ADD CONSTRAINT `ck_schedules_hours` CHECK (end_time > start_time);

CREATE UNIQUE INDEX `ix_course_schedules_section_id_subject_id` ON `course_schedules` (`section_id`, `subject_id`);

CREATE INDEX `ix_course_schedules_subject_id` ON `course_schedules` (`subject_id`);

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261013081617_AddCourseSchedules', '9.0.20');

ALTER TABLE `subjects` ADD `units` decimal(3,1) NOT NULL DEFAULT 3.0;

ALTER TABLE `subjects` MODIFY COLUMN `units` decimal(3,1) NOT NULL;

ALTER TABLE `subjects` ADD CONSTRAINT `ck_subjects_units` CHECK (units >= 0.5 AND units <= 12);

ALTER TABLE `enrollments` ADD `name_extension` varchar(10) CHARACTER SET utf8mb4 NULL;

ALTER TABLE `course_schedules` ADD `schedule_id` int NULL;

UPDATE `course_schedules` c JOIN `sections` s ON s.`id` = c.`section_id` SET c.`schedule_id` = s.`schedule_id` WHERE s.`schedule_id` IS NOT NULL AND c.`id` > 0;

UPDATE `course_schedules` c SET c.`schedule_id` = (SELECT sc.`id` FROM `schedules` sc WHERE c.`start_time` >= sc.`start_time` AND c.`end_time` <= sc.`end_time` ORDER BY sc.`id` LIMIT 1) WHERE c.`schedule_id` IS NULL AND c.`id` > 0;

UPDATE `course_schedules` c SET c.`schedule_id` = (SELECT MIN(sc.`id`) FROM `schedules` sc) WHERE c.`schedule_id` IS NULL AND c.`id` > 0;

ALTER TABLE `course_schedules` MODIFY COLUMN `schedule_id` int NOT NULL;

CREATE INDEX `ix_course_schedules_schedule_id` ON `course_schedules` (`schedule_id`);

ALTER TABLE `course_schedules` ADD CONSTRAINT `fk_course_schedules_schedules_schedule_id` FOREIGN KEY (`schedule_id`) REFERENCES `schedules` (`id`) ON DELETE RESTRICT;

ALTER TABLE `sections` DROP FOREIGN KEY `fk_sections_schedules_schedule_id`;

ALTER TABLE `sections` DROP FOREIGN KEY `fk_sections_strand_schedules_strand_id_schedule_id`;

CREATE TEMPORARY TABLE `tmp_section_rank` AS
SELECT u.`id`, u.`used`,
       ROW_NUMBER() OVER (PARTITION BY u.`school_year_id`, u.`strand_id`, u.`grade_level`, u.`name` ORDER BY u.`used` DESC, u.`id`) AS `place`
FROM (SELECT s.`id`, s.`school_year_id`, s.`strand_id`, s.`grade_level`, s.`name`,
             (EXISTS (SELECT 1 FROM `students` x WHERE x.`section_id` = s.`id`)
              OR EXISTS (SELECT 1 FROM `teachers` x WHERE x.`section_id` = s.`id`)
              OR EXISTS (SELECT 1 FROM `enrollments` x WHERE x.`section_id` = s.`id`)
              OR EXISTS (SELECT 1 FROM `teaching_assignments` x WHERE x.`section_id` = s.`id`)
              OR EXISTS (SELECT 1 FROM `announcements` x WHERE x.`section_id` = s.`id`)) AS `used`
      FROM `sections` s) u;

DELETE FROM `sections` WHERE `id` IN (SELECT `id` FROM `tmp_section_rank` WHERE `place` > 1 AND `used` = 0) AND `id` > 0 LIMIT 100000;

UPDATE `sections` SET `name` = CONCAT(LEFT(`name`, 44), '-', COALESCE((SELECT sc.`code` FROM `schedules` sc WHERE sc.`id` = `sections`.`schedule_id`), CAST(`id` AS CHAR)))
WHERE `id` IN (SELECT `id` FROM `tmp_section_rank` WHERE `place` > 1 AND `used` = 1) AND `id` > 0 LIMIT 100000;

DROP TEMPORARY TABLE `tmp_section_rank`;

DROP TABLE `strand_schedules`;

CREATE UNIQUE INDEX `ix_sections_school_year_id_strand_id_grade_level_name` ON `sections` (`school_year_id`, `strand_id`, `grade_level`, `name`);

CREATE INDEX `ix_sections_strand_id` ON `sections` (`strand_id`);

ALTER TABLE `sections` DROP INDEX `ix_sections_schedule_id`;

ALTER TABLE `sections` DROP INDEX `ix_sections_school_year_id_strand_id_grade_level_schedule_id_na`;

ALTER TABLE `sections` DROP INDEX `ix_sections_strand_id_schedule_id`;

ALTER TABLE `sections` DROP COLUMN `schedule_id`;

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261013161231_UnitsNameExtensionAndClassTimeSchedules', '9.0.20');

ALTER TABLE `attachments` ADD `submission_id` int NULL;

CREATE TABLE `final_grades` (
    `id` int NOT NULL AUTO_INCREMENT,
    `assignment_id` int NOT NULL,
    `student_id` int NOT NULL,
    `grade` decimal(5,2) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_final_grades` PRIMARY KEY (`id`),
    CONSTRAINT `ck_final_grades_grade` CHECK (grade >= 0 AND grade <= 100),
    CONSTRAINT `fk_final_grades_students_student_id` FOREIGN KEY (`student_id`) REFERENCES `students` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_final_grades_teaching_assignments_assignment_id` FOREIGN KEY (`assignment_id`) REFERENCES `teaching_assignments` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `ix_attachments_submission_id` ON `attachments` (`submission_id`);

CREATE UNIQUE INDEX `ix_final_grades_assignment_id_student_id` ON `final_grades` (`assignment_id`, `student_id`);

CREATE INDEX `ix_final_grades_student_id` ON `final_grades` (`student_id`);

ALTER TABLE `attachments` ADD CONSTRAINT `fk_attachments_submissions_submission_id` FOREIGN KEY (`submission_id`) REFERENCES `submissions` (`id`) ON DELETE CASCADE;

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261013162615_AddFinalGradesAndSubmissionFiles', '9.0.20');

CREATE TABLE `calendar_entry_invites` (
    `entry_id` int NOT NULL,
    `user_id` int NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    CONSTRAINT `pk_calendar_entry_invites` PRIMARY KEY (`entry_id`, `user_id`),
    CONSTRAINT `fk_calendar_entry_invites_calendar_entries_entry_id` FOREIGN KEY (`entry_id`) REFERENCES `calendar_entries` (`id`) ON DELETE CASCADE,
    CONSTRAINT `fk_calendar_entry_invites_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

CREATE INDEX `ix_calendar_entry_invites_user_id` ON `calendar_entry_invites` (`user_id`);

INSERT INTO `course_schedules` (`section_id`, `subject_id`, `schedule_id`, `days`, `start_time`, `end_time`, `created_at_utc`, `updated_at_utc`)
SELECT r.`section_id`, r.`subject_id`, r.`schedule_id`,
       IF(MOD(r.`place` + r.`k`, 2) = 1, 'MWF', 'TTH'),
       ADDTIME(r.`window_start`, SEC_TO_TIME(MOD(FLOOR((r.`place` - 1) / 2) + r.`k`, r.`hours`) * 3600)),
       ADDTIME(r.`window_start`, SEC_TO_TIME((MOD(FLOOR((r.`place` - 1) / 2) + r.`k`, r.`hours`) + 1) * 3600)),
       UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
FROM (
    SELECT c.`section_id`, c.`subject_id`, c.`place`, c.`k`, sc.`id` AS `schedule_id`, sc.`start_time` AS `window_start`,
           GREATEST(1, FLOOR(TIME_TO_SEC(TIMEDIFF(sc.`end_time`, sc.`start_time`)) / 3600)) AS `hours`
    FROM (
        SELECT s.`id` AS `section_id`, ss.`subject_id`,
               ROW_NUMBER() OVER (PARTITION BY s.`id`, ss.`term_number` ORDER BY ss.`subject_id`) AS `place`,
               DENSE_RANK() OVER (ORDER BY s.`id`) AS `k`,
               CASE LEFT(s.`name`, 1) WHEN 'B' THEN 'MID' WHEN 'C' THEN 'PM' ELSE 'AM' END AS `part`
        FROM `sections` s
        JOIN (SELECT `strand_id`, `grade_level`, `subject_id`, MIN(`term_number`) AS `term_number` FROM `strand_subjects` GROUP BY `strand_id`, `grade_level`, `subject_id`) ss ON ss.`strand_id` = s.`strand_id` AND ss.`grade_level` = s.`grade_level`
        WHERE NOT EXISTS (SELECT 1 FROM `course_schedules` x WHERE x.`section_id` = s.`id`)
          AND NOT EXISTS (SELECT 1 FROM `teaching_assignments` t WHERE t.`section_id` = s.`id`)
    ) c
    JOIN `schedules` sc ON sc.`code` = c.`part`
) r
WHERE r.`place` <= 2 * r.`hours`
  AND r.`window_start` IS NOT NULL;

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261014032339_ClassTimesForSeededSectionsAndSharedEvents', '9.0.20');

UPDATE `system_settings` SET `value` = 'SEAITT Legazpi' WHERE `key` = 'school.short_name' AND `value` = 'SEAITT';

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261015055658_SchoolNameSeaittLegazpi', '9.0.20');

UPDATE `system_settings` SET `value` = '10' WHERE `key` = 'signin.lockout_minutes' AND `value` = '15';

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261015060110_LockoutTenMinutes', '9.0.20');

UPDATE `system_settings` SET `value` = '5' WHERE `key` = 'signin.lockout_minutes' AND `value` IN ('10', '15');

INSERT INTO `__EFMigrationsHistory` (`migration_id`, `product_version`)
VALUES ('20261015063534_LockoutFiveMinutes', '9.0.20');

COMMIT;

