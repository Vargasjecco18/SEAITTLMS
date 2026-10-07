-- Sample sign-ins for SEAITT e-LMS: one administrator, one registrar, one guidance counselor and one student.
--
-- How to run it: open this file in MySQL Workbench (File > Open SQL Script...), then press the lightning bolt (Execute).
-- The tables must exist already (start the app once, or run seaittlms_reset_and_rebuild.sql first).
-- It is safe to run again: an account is only added when it is not there yet. The school has exactly ONE administrator, ONE
-- registrar and ONE guidance counselor, so for those roles nothing is added when the role already has an account
-- (the app makes the first administrator by itself from the Seed:Admin settings in appsettings.Local.json).
--
-- The passwords are not in this file, only their hashes (the same kind the app writes). Give the passwords to the people
-- and ask them to change them (an administrator can set a new one in Users Management > Edit).

USE seaittlms;

SET @now = UTC_TIMESTAMP(6);

-- Administrator: admin@legazpi.seaitt.edu.ph
INSERT INTO users (email, password_hash, phone_number, role_id, is_active, security_stamp, failed_login_count, created_at_utc, updated_at_utc)
SELECT 'admin@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$KBVJnDlBZOS2exKAy6vrIw==$fRoMz//mUka2MBVpy19OqiBHjAQxZockjL0YnlrQ32M=', NULL, 1, 1, 'e64cd3602abe484cba42c3aa714fd33f', 0, @now, @now FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM users WHERE role_id = 1) AND NOT EXISTS (SELECT 1 FROM users WHERE email = 'admin@legazpi.seaitt.edu.ph');
INSERT INTO administrators (user_id, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, 'Maria', 'Cristina', 'Santos', 'Active', @now, @now FROM users u
WHERE u.email = 'admin@legazpi.seaitt.edu.ph' AND u.role_id = 1 AND NOT EXISTS (SELECT 1 FROM administrators p WHERE p.user_id = u.id);

-- Registrar: registrar@legazpi.seaitt.edu.ph
INSERT INTO users (email, password_hash, phone_number, role_id, is_active, security_stamp, failed_login_count, created_at_utc, updated_at_utc)
SELECT 'registrar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ZwubAjO4wlCc4hP3yZIkLQ==$XWYIX2M3/hkvuJ6Dxe8WYjgkQY1UsKpRLOox9bOLOrc=', NULL, 4, 1, '2364565349df424c898050ebddb223f8', 0, @now, @now FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM users WHERE role_id = 4) AND NOT EXISTS (SELECT 1 FROM users WHERE email = 'registrar@legazpi.seaitt.edu.ph');
INSERT INTO registrars (user_id, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, 'Rosa', NULL, 'Mendoza', 'Active', @now, @now FROM users u
WHERE u.email = 'registrar@legazpi.seaitt.edu.ph' AND u.role_id = 4 AND NOT EXISTS (SELECT 1 FROM registrars p WHERE p.user_id = u.id);

-- Guidance counselor: guidance@legazpi.seaitt.edu.ph
INSERT INTO users (email, password_hash, phone_number, role_id, is_active, security_stamp, failed_login_count, created_at_utc, updated_at_utc)
SELECT 'guidance@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$qNe9/U2DYU+/qs0VsrjIIw==$o91LCJ0cp4ukl2GZ03EBHKCYEkLToTGrUkiYxT5+GTY=', NULL, 5, 1, '760403fbf39945b8b3e2dcae2457ed4c', 0, @now, @now FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM users WHERE role_id = 5) AND NOT EXISTS (SELECT 1 FROM users WHERE email = 'guidance@legazpi.seaitt.edu.ph');
INSERT INTO guidance_counselors (user_id, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, 'Gloria', NULL, 'Reyes', 'Active', @now, @now FROM users u
WHERE u.email = 'guidance@legazpi.seaitt.edu.ph' AND u.role_id = 5 AND NOT EXISTS (SELECT 1 FROM guidance_counselors p WHERE p.user_id = u.id);

-- Student: delacruz.789012@legazpi.seaitt.edu.ph
INSERT INTO users (email, password_hash, phone_number, role_id, is_active, security_stamp, failed_login_count, created_at_utc, updated_at_utc)
SELECT 'delacruz.789012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$C0ZlXdt5/9JV2NeZ5YpEEQ==$VkjVE/ekjaanlWPkFo8bCdpaWaDJ/gpsxYcNpx7a4Bk=', NULL, 3, 1, '77aabf2e121f4ea7a563152f2eb77af2', 0, @now, @now FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM users WHERE email = 'delacruz.789012@legazpi.seaitt.edu.ph') AND NOT EXISTS (SELECT 1 FROM students WHERE lrn = '123456789012');
INSERT INTO students (user_id, lrn, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, '123456789012', 'Juan', 'Santos', 'Dela Cruz', 'Active', @now, @now FROM users u
WHERE u.email = 'delacruz.789012@legazpi.seaitt.edu.ph' AND u.role_id = 3 AND NOT EXISTS (SELECT 1 FROM students s WHERE s.user_id = u.id);

-- What is in the database now:
SELECT r.name AS role, u.email, u.is_active FROM users u JOIN roles r ON r.id = u.role_id WHERE u.role_id IN (1, 3, 4, 5) ORDER BY u.role_id, u.id;
