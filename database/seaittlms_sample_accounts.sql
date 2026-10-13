-- Sample sign-ins for SEAITT e-LMS: 1 administrator, 1 registrar, 1 guidance counselor, 1 academic coordinator, 1 academic provider (central team), 6 teachers and 12 students.
--
-- The sign-ins are easy on purpose, to try the system: the email is the last name (or the role) + @legazpi.seaitt.edu.ph and the password is
-- Teacher1, Teacher2, ... for the teachers, Student1, Student2, ... for the students, and Registrar1, Guidance1, Coordinator1, Provider1, Admin123.
-- For example: vargas@legazpi.seaitt.edu.ph with the password Teacher1. (The list with every name, email and password is kept out of this file.)
-- Give real people a harder password: an administrator can set a new one in Users Management > Edit.
--
-- How to run it: open this file in MySQL Workbench (File > Open SQL Script...), then press the lightning bolt (Execute).
-- The tables must exist already (start the app once, or run seaittlms_reset_and_rebuild.sql first).
-- It is safe to run again, and it is safe to run over the sample accounts of an earlier version of this file:
--   * a person who is not there yet is added;
--   * a person who is there already (with the email of this file or of the earlier one) gets the email and the password of this file, and keeps
--     everything else (courses, enrollment, grades). The sign-in of a locked account is opened.
-- The school has exactly ONE administrator, ONE registrar, ONE guidance counselor, ONE academic coordinator and ONE academic provider, so for those roles nothing is added when the role already
-- has an account (the app makes the first administrator by itself from the Seed:Admin settings in appsettings.Local.json, with the password you
-- chose there). THE PASSWORD OF AN ADMINISTRATOR IS NEVER CHANGED by this file. To give that administrator the password Admin123 too, run the
-- last statement of this file (it is written as a comment).
--
-- The teachers and the students are plain accounts. They have no courses or enrollments yet: the administrator gives a teacher
-- courses (Users Management > Teachers > Courses), and the registrar enrolls the students (Enrollments, type the LRN: the account
-- is found). Until then the students are listed under Not enrolled (a card of the administrator's dashboard).
--
-- The passwords are not in this file, only their hashes (the same kind the app writes).

USE seaittlms;

SET @now = UTC_TIMESTAMP(6);

-- The people to add, in a temporary table that disappears when the connection closes. It has no character set of its own,
-- so its text columns get the database's, the same as the real tables they are compared with.
DROP TEMPORARY TABLE IF EXISTS sample_people;
CREATE TEMPORARY TABLE sample_people (
    role_id INT NOT NULL, email VARCHAR(254) NOT NULL, old_email VARCHAR(254) NOT NULL, password_hash VARCHAR(255) NOT NULL, security_stamp VARCHAR(64) NOT NULL,
    first_name VARCHAR(100) NOT NULL, middle_name VARCHAR(100) NULL, last_name VARCHAR(100) NOT NULL, employee_number VARCHAR(30) NULL, lrn CHAR(12) NULL, name_extension VARCHAR(10) NULL
);

INSERT INTO sample_people (role_id, email, old_email, password_hash, security_stamp, first_name, middle_name, last_name, employee_number, lrn, name_extension) VALUES
    (1, 'admin@legazpi.seaitt.edu.ph', 'admin@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$w4rehiwFx/I8UmA35o/Wcg==$SBhvhskoyqSs5/r13cWRntRtJbwAElTTVmCOld+dohI=', '2edac06a4a904e6ab1ed9ac1a7eebce8', 'Maria', 'Cristina', 'Santos', 'A-001', NULL, NULL),
    (4, 'registrar@legazpi.seaitt.edu.ph', 'registrar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$P6Sf4zv+H/VbzINBGQYP9A==$TKLSRqwKJ+K5RSdNjvmAqb0DixRNhBhBjRbtV7dlMMg=', '91614da4264242fc8b68e21ff37cebac', 'Rosa', NULL, 'Mendoza', 'R-001', NULL, NULL),
    (5, 'guidance@legazpi.seaitt.edu.ph', 'guidance@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$yH08xOAHRvVt8KC4Sl711w==$22gD2Ae090ScjbTVZWYKouTWzPEmF4gqApw0K1/cTZk=', '24e3efae5ccb4de4bccc7a47ef692f65', 'Gloria', NULL, 'Reyes', 'G-001', NULL, NULL),
    (6, 'coordinator1@legazpi.seaitt.edu.ph', 'coordinator@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$VOTxuqG9e01kIoj6RYK6DQ==$G2A8hqmWi5uujPEehuZY1q5nBhgJ92AJiZbMEU2hbng=', 'af7c110fc75d469498587125cab4da82', 'Lourdes', 'Garcia', 'Villareal', 'C-001', NULL, NULL),
    (7, 'provider1@legazpi.seaitt.edu.ph', 'provider1@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$2WqDh+UcW7Zh46bvYYxi/Q==$HXc0s6R7KVsjvhqXEhZuuXXmAj2XmqNRVQxPCXrPCUE=', 'c28f5f5ff8cb4c44876e823f5510a2b4', 'Elena', 'Cruz', 'Pangilinan', 'P-001', NULL, NULL),
    (2, 'vargas@legazpi.seaitt.edu.ph', 'vargas@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$VCismY/w/3s3a0qyQ9gnWA==$OA+nsaI5M7QvY4GinejfOjiYh3usv+rWTpAtnTI+XP8=', 'f6d02c354e02439eaaa0d817c92f5312', 'Rafael', 'Mendoza', 'Vargas', 'T-013', NULL, NULL),
    (2, 'villanueva@legazpi.seaitt.edu.ph', 'ricardo.villanueva@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$8LQZYVadiN0Tbm4lNiVtoA==$oZJnKZR9TH2A/z5MGSLuVDA8OxL92juGm5w6121tc8s=', 'f4e5b764ae0945d3a36ed9a7599ed9e7', 'Ricardo', 'Dizon', 'Villanueva', 'T-001', NULL, NULL),
    (2, 'bautista@legazpi.seaitt.edu.ph', 'angelica.bautista@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ZpZ2R7xAuaOp+7c6KKY0YQ==$fh72qkeXQ6QE5TlLC3Z988cRM8JOV14sD/v1bRVPndo=', '6c67b0d6894a48c68ee4c2d0de6beae3', 'Angelica', 'Lopez', 'Bautista', 'T-002', NULL, NULL),
    (2, 'navarro@legazpi.seaitt.edu.ph', 'eduardo.navarro@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$/6Zz9Ci3BxvcFjOkAxZ0MA==$YIF1kmVUUW+06nWtMZY5ZgUW/ilD9PALDqoVUgk1z9o=', '92c5c8f34cdb4a5cb26f401d6391057b', 'Eduardo', NULL, 'Navarro', 'T-003', NULL, NULL),
    (2, 'domingo@legazpi.seaitt.edu.ph', 'marites.domingo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$kiCJrPDqN6v/DjDfk5lnYw==$bA6OVYSx58IRAQ3ORMpsPUjtJ6bQCtu6mMiciTdefJg=', 'faf72884a993409bbafa491efe7934eb', 'Marites', 'Cruz', 'Domingo', 'T-004', NULL, NULL),
    (2, 'aquino@legazpi.seaitt.edu.ph', 'jonathan.aquino@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$jajoJb1IteIzKs9ZsKFPcA==$R8CFsLEBVtNq0fSxMgLMt/rVHN8NyqS5ES17kloOP3U=', '0646a742045c417d83ba4d00d0f253d2', 'Jonathan', NULL, 'Aquino', 'T-005', NULL, NULL),
    (3, 'delacruz@legazpi.seaitt.edu.ph', 'delacruz.789012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$4jFJpVOnMRCMghe6Rgyx7w==$cid9ChUv9qAKRhtXrPi3G8yTdZHzTbtDN6hpfsw+3tI=', 'c755f2dbbcac472fb8c6518b299186f1', 'Juan', 'Santos', 'Dela Cruz', NULL, '123456789012', NULL),
    (3, 'abad@legazpi.seaitt.edu.ph', 'abad.600001@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$lNJENaoZIcVXDYHf4uFtxA==$YVxn1RuQxWwkxz/xW9Au5VwycunsCeCNi+Sow0FXGvA=', 'ae5b3050c41442a6ba40ce71b98a79d1', 'Mariel', 'Aguilar', 'Abad', NULL, '105202600001', NULL),
    (3, 'alcantara@legazpi.seaitt.edu.ph', 'alcantara.600002@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$WLNDi7gI6fcOn+olOQzJ9Q==$Ilh5UysUfGxb27hKxyHoWi0YECv/1mQmjTB3SF7eUew=', '76cc01edbdf749d88475576d62717cf5', 'Carlo', 'Bautista', 'Alcantara', NULL, '105202600002', NULL),
    (3, 'balagtas@legazpi.seaitt.edu.ph', 'balagtas.600005@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$g8s45PWw4BNnGkOlaLmbLQ==$Muhbu7+THjxdFK8wf4lAAJxfuAL4w9eM8a5KhbDrtSE=', '074e950bec45451cb528bd5417509cfb', 'Hannah', 'Estrada', 'Balagtas', NULL, '105202600005', NULL),
    (3, 'barrientos@legazpi.seaitt.edu.ph', 'barrientos.600006@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$x4Ii6ulr2VvC3dvhgDN9AQ==$cMEC/4uQsvWOPnwki+Q1nzW49g72XU7fPloPanWW/tk=', '0bf370f6ea3d441886f2f3debd966ede', 'Paolo', 'Flores', 'Barrientos', NULL, '105202600006', 'Jr.'),
    (3, 'buenaventura@legazpi.seaitt.edu.ph', 'buenaventura.600007@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$aeN2GLxnkbFqN25d7ZVd+Q==$uZ4pYpa6KeQN2KioDmEz22QvCVVkDkr2ewjfnL7zcpE=', '8615c52b813847f1af8534502c572196', 'Kristine', 'Gomez', 'Buenaventura', NULL, '105202600007', NULL),
    (3, 'dacillo@legazpi.seaitt.edu.ph', 'dacillo.600010@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$PONHZwN4EzEb7QJQUC/ntA==$qQWixSplv5DDjDdec7txNWd7TKIwI2wp3uOc2uYGBK0=', '80d9b45cc93146ce9590df0b80fdb711', 'Nathan', 'Jimenez', 'Dacillo', NULL, '105202600010', NULL),
    (3, 'dumalag@legazpi.seaitt.edu.ph', 'dumalag.600011@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ukGjStlnYpK0ci4Ee5fEKg==$whmJOR4JHbXe7y67d6o7uta622sC09myS7qHiX4GzHs=', '49f07cf0140e431695331c0dcb766d23', 'Bea', 'Katigbak', 'Dumalag', NULL, '105202600011', NULL),
    (3, 'escalante@legazpi.seaitt.edu.ph', 'escalante.600012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$A981wp8/3rFkZtiQkK3XdA==$n3sZ1Y0SAS/BA4kgLjRjCyLgi4D+av4igonU1SIuisI=', '0ff08dac25894bdca2caad7151469534', 'Ryan', 'Lacson', 'Escalante', NULL, '105202600012', NULL),
    (3, 'gonzales@legazpi.seaitt.edu.ph', 'gonzales.600015@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$zeMgNUpovFSoiXmrHfl4tg==$E+Y/BNf5VLJyN0OHm1oOSbnAFIfCPDsUAis3uatZRdU=', '02c8263fb734424a8a8f23f077ba0783', 'Trisha', 'Ong', 'Gonzales', NULL, '105202600015', NULL),
    (3, 'guevarra@legazpi.seaitt.edu.ph', 'guevarra.600016@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$IbwJiT8k2mkRQ9OORkrT2A==$NkXGhR0df7zvHIQ30wFNpUDT+DFWetgfiySo4GDR0n8=', '3a2ec08e470840ff8b0a2088a806e92d', 'Kevin', 'Pineda', 'Guevarra', NULL, '105202600016', NULL),
    (3, 'ibanez@legazpi.seaitt.edu.ph', 'ibanez.600017@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Q4BW/V9R6650WIE9x3SDjw==$0wBu++khGKR1PXAZ6+opxIEVSjbnj9mEJ5tRXFVRpbc=', '3e540f8efbfc40aba45259d71d30b663', 'Alyssa', 'Quizon', 'Ibanez', NULL, '105202600017', NULL);

-- 1) The accounts that are not there yet. Role 1 = administrator, 2 = teacher, 3 = student, 4 = registrar, 5 = guidance counselor, 6 = academic coordinator, 7 = academic provider.
INSERT INTO users (email, password_hash, phone_number, role_id, is_active, security_stamp, failed_login_count, created_at_utc, updated_at_utc)
SELECT p.email, p.password_hash, NULL, p.role_id, 1, p.security_stamp, 0, @now, @now
FROM sample_people p
WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u.email = p.email OR u.email = p.old_email)
  AND (p.role_id NOT IN (1, 4, 5, 6, 7) OR NOT EXISTS (SELECT 1 FROM users u WHERE u.role_id = p.role_id))
  AND (p.lrn IS NULL OR NOT EXISTS (SELECT 1 FROM students s WHERE s.lrn = p.lrn));

-- 2) The accounts that are there already (made by an earlier version of this file) get the new email and password, and are signed out.
--    An administrator's is not touched. An email that another account has already is not taken from it.
UPDATE users u
JOIN sample_people p ON (u.email = p.old_email OR u.email = p.email) AND u.role_id = p.role_id
SET u.email = p.email, u.password_hash = p.password_hash, u.security_stamp = p.security_stamp, u.failed_login_count = 0, u.lockout_end_utc = NULL, u.updated_at_utc = @now
WHERE u.id > 0 AND p.role_id <> 1
  AND NOT EXISTS (SELECT 1 FROM (SELECT id, email FROM users) x WHERE x.email = p.email AND x.id <> u.id);

-- 3) The profile of each account: the row with the name, in the table of its role. (A person who has one already keeps it.)
INSERT INTO administrators (user_id, employee_number, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, p.employee_number, p.first_name, p.middle_name, p.last_name, 'Active', @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 1 AND NOT EXISTS (SELECT 1 FROM administrators x WHERE x.user_id = u.id)
  AND NOT EXISTS (SELECT 1 FROM administrators y WHERE y.employee_number = p.employee_number);

INSERT INTO registrars (user_id, employee_number, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, p.employee_number, p.first_name, p.middle_name, p.last_name, 'Active', @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 4 AND NOT EXISTS (SELECT 1 FROM registrars x WHERE x.user_id = u.id)
  AND NOT EXISTS (SELECT 1 FROM registrars y WHERE y.employee_number = p.employee_number);

INSERT INTO guidance_counselors (user_id, employee_number, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, p.employee_number, p.first_name, p.middle_name, p.last_name, 'Active', @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 5 AND NOT EXISTS (SELECT 1 FROM guidance_counselors x WHERE x.user_id = u.id)
  AND NOT EXISTS (SELECT 1 FROM guidance_counselors y WHERE y.employee_number = p.employee_number);

INSERT INTO academic_coordinators (user_id, employee_number, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, p.employee_number, p.first_name, p.middle_name, p.last_name, 'Active', @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 6 AND NOT EXISTS (SELECT 1 FROM academic_coordinators x WHERE x.user_id = u.id)
  AND NOT EXISTS (SELECT 1 FROM academic_coordinators y WHERE y.employee_number = p.employee_number);

INSERT INTO academic_providers (user_id, employee_number, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, p.employee_number, p.first_name, p.middle_name, p.last_name, 'Active', @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 7 AND NOT EXISTS (SELECT 1 FROM academic_providers x WHERE x.user_id = u.id)
  AND NOT EXISTS (SELECT 1 FROM academic_providers y WHERE y.employee_number = p.employee_number);

INSERT INTO teachers (user_id, employee_number, first_name, middle_name, last_name, created_at_utc, updated_at_utc)
SELECT u.id, p.employee_number, p.first_name, p.middle_name, p.last_name, @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 2 AND NOT EXISTS (SELECT 1 FROM teachers x WHERE x.user_id = u.id)
  AND NOT EXISTS (SELECT 1 FROM teachers y WHERE y.employee_number = p.employee_number);

INSERT INTO students (user_id, lrn, first_name, middle_name, last_name, name_extension, status, created_at_utc, updated_at_utc)
SELECT u.id, p.lrn, p.first_name, p.middle_name, p.last_name, p.name_extension, 'Active', @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 3 AND NOT EXISTS (SELECT 1 FROM students x WHERE x.user_id = u.id)
  AND NOT EXISTS (SELECT 1 FROM students y WHERE y.lrn = p.lrn);

DROP TEMPORARY TABLE sample_people;

-- What is in the database now: how many accounts each role has.
SELECT r.name AS role, COUNT(*) AS accounts FROM users u JOIN roles r ON r.id = u.role_id GROUP BY r.id, r.name ORDER BY r.id;

-- And the sign-in names of the people of this file.
SELECT r.name AS role, u.email, u.is_active FROM users u JOIN roles r ON r.id = u.role_id ORDER BY u.role_id, u.id;

-- OPTIONAL: to give the administrator the password Admin123 as well (only for trying the system), remove the two dashes in front of the lines below
-- and run them. Use the email of your administrator.
-- UPDATE users SET password_hash = 'PBKDF2-SHA256$600000$rx6XyO6wWnxzwKzdkRyYTQ==$apAfvI0zZgKkmBMBOK6U1/7vi9h4aw38Ndc9fYfcyrw=', security_stamp = 'd1c70abf3f014d119e5dbd49ed372445', failed_login_count = 0, lockout_end_utc = NULL
--  WHERE role_id = 1 AND email = 'admin@legazpi.seaitt.edu.ph' AND id > 0;
