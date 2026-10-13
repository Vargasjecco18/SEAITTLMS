-- Sample sign-ins for SEAITT e-LMS: 1 administrator, 1 registrar, 1 guidance counselor, 1 academic coordinator, 1 academic provider (central team), 10 teachers and 20 students.
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
    first_name VARCHAR(100) NOT NULL, middle_name VARCHAR(100) NULL, last_name VARCHAR(100) NOT NULL, employee_number VARCHAR(30) NULL, lrn CHAR(12) NULL
);

INSERT INTO sample_people (role_id, email, old_email, password_hash, security_stamp, first_name, middle_name, last_name, employee_number, lrn) VALUES
    (1, 'admin@legazpi.seaitt.edu.ph', 'admin@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$9Ecv8sSXvBdFuXUcoX0OrQ==$iSpAjimI+fjkKios3HIqFZDN88f/lvVduz5NBRs0FWg=', 'b16a7461d5744ef6a54c553749c2213f', 'Maria', 'Cristina', 'Santos', 'A-001', NULL),
    (4, 'registrar@legazpi.seaitt.edu.ph', 'registrar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$SRC/HXGcorg2kBCA/qaZvQ==$8d1BxuYAWzOAtdaiIofPE0DzLkvQZIL0r74uJhA5gXY=', 'c037ab58c11c4185b4dc73f49f0a0406', 'Rosa', NULL, 'Mendoza', 'R-001', NULL),
    (5, 'guidance@legazpi.seaitt.edu.ph', 'guidance@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$XrIRQV0q4fwa0QTANEikTQ==$9yJHdVGz+kdgDj9DJAv0cdCUEKxmG/LrcwLmI+DnNvU=', '1278cc944d364dd2a43458be441f76e5', 'Gloria', NULL, 'Reyes', 'G-001', NULL),
    (6, 'coordinator1@legazpi.seaitt.edu.ph', 'coordinator@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Eg3/BBYk8QPJW+fxJ17YLQ==$e29qCPYHUS21zhMLRn6ynNSRP2WwLLhf+f0QkBx88tI=', 'e238da781ff24c59bc6b67064a343d97', 'Lourdes', 'Garcia', 'Villareal', 'C-001', NULL),
    (7, 'provider1@legazpi.seaitt.edu.ph', 'provider1@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$MH/Wx24RG/PFi3C0nU8NGw==$Z446OgLVzvjemf+beXaNBpVl7EgMvSnSRDsEe9485UU=', 'f6d6e1170a8448ac9f154016393b6d25', 'Elena', 'Cruz', 'Pangilinan', 'P-001', NULL),
    (2, 'vargas@legazpi.seaitt.edu.ph', 'vargas@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$OcD03EkPNdN6/L4hcx4fTw==$+zorOUurcoltUBI/FiHenTClSrip219pOcHXj+KjAFo=', '28bd25df237e468e843e0277a0b62d20', 'Rafael', 'Mendoza', 'Vargas', 'T-013', NULL),
    (2, 'villanueva@legazpi.seaitt.edu.ph', 'ricardo.villanueva@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$BjXLGfM9Y3RCH6j57Qjudg==$zQbbUBwIA3lV66q7hJL5RByWBuiHjXxrZLUymCWlBY8=', 'e381c0de9ab644489ec63663f8617524', 'Ricardo', 'Dizon', 'Villanueva', 'T-001', NULL),
    (2, 'bautista@legazpi.seaitt.edu.ph', 'angelica.bautista@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ICbEFQKL6JMxyvbsDorBMA==$0ELTyAGz9YnZtd6lhnbMLk0cVml53yne1v/PCZr3ByI=', 'b870d73b2f7d43989f747aec5159b78c', 'Angelica', 'Lopez', 'Bautista', 'T-002', NULL),
    (2, 'navarro@legazpi.seaitt.edu.ph', 'eduardo.navarro@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$tEcjoxf6zGkt4oQuNYG6Yw==$i07121txwu3n5BLkhM6w/VH1tBsCZjo4HwHBmKe7+3s=', '0f81d2380d7342b98f13d4295bcb8e5c', 'Eduardo', NULL, 'Navarro', 'T-003', NULL),
    (2, 'domingo@legazpi.seaitt.edu.ph', 'marites.domingo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$7kk7D86ySgWMfg5J8X6rMQ==$dpftoM8aeaDKw2XJyJKmrz9fl8QWuiqUG395k4A6duc=', '8239511642554660b17f43109adda302', 'Marites', 'Cruz', 'Domingo', 'T-004', NULL),
    (2, 'aquino@legazpi.seaitt.edu.ph', 'jonathan.aquino@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$vb5o3jk8gH9RzRkY1wikRg==$i4OUw7wwTGPSQ2FiBGyqujd+f83YGjkMUEzJYTF030g=', '52d2de6fe363462daecbe236fd42d0b4', 'Jonathan', NULL, 'Aquino', 'T-005', NULL),
    (2, 'fernandez@legazpi.seaitt.edu.ph', 'cristina.fernandez@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$RWz5foXSK7rszs9jJZdUzA==$rrm2xgMTVcSq4NN8oTFAkS3E2gZUrmfZtdrSToUZ6eo=', 'ba83d3d5ea094ac5939063d58f762b58', 'Cristina', 'Reyes', 'Fernandez', 'T-006', NULL),
    (2, 'castillo@legazpi.seaitt.edu.ph', 'roberto.castillo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$v5xNXQKOz5uGoLeiGK7FiA==$xra7ckZBv+IqrWFAfT4iz9arD+M366YaaQ3uwFCxmEU=', '8a2c6347074b43598a9496506b8215e8', 'Roberto', 'Garcia', 'Castillo', 'T-007', NULL),
    (2, 'pascual@legazpi.seaitt.edu.ph', 'lorna.pascual@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$d+XSRvUUwPUTtcRTOyj/LA==$OWYkbOsTa/C19cSKgpTrY1c+Ym/0PtLbjXCXBobxdok=', '95921f41e03e41d5900cdc970933ad50', 'Lorna', 'Santos', 'Pascual', 'T-008', NULL),
    (2, 'soriano@legazpi.seaitt.edu.ph', 'gregorio.soriano@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$5axC6FJF4qu2j5xAUgUqRg==$QT12lnCvhx1cuecRuL7tt721wOjoBkI7tDXrJAW4D74=', '355cd76210904e7ea101ab1d64fb7f64', 'Gregorio', NULL, 'Soriano', 'T-009', NULL),
    (3, 'delacruz@legazpi.seaitt.edu.ph', 'delacruz.789012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$GSWfVeVrs21Q7Vog9xSyUg==$cJRjBU/54i3mvc9wqkgjQO6QNunA8/fLeUe/ku7x2LE=', '5c378e0042074d9db45889f97d15336b', 'Juan', 'Santos', 'Dela Cruz', NULL, '123456789012'),
    (3, 'abad@legazpi.seaitt.edu.ph', 'abad.600001@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ScWz/usrjeJrCTmprR2S/A==$NJg4yNdwTJdXujDtSKGGwze8U6I+4XNAYuEes04n1Ww=', '30880148cb1c4f79bc5090d137681b74', 'Mariel', 'Aguilar', 'Abad', NULL, '105202600001'),
    (3, 'alcantara@legazpi.seaitt.edu.ph', 'alcantara.600002@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$+vdeXmD1gBKKMVfGIKra5Q==$itig7iGMDF5suNYY2UwqnZL1awXpkH9c7PjnbUzFIl4=', '102af7b24ea44350b613d9d1225130a2', 'Carlo', 'Bautista', 'Alcantara', NULL, '105202600002'),
    (3, 'andrada@legazpi.seaitt.edu.ph', 'andrada.600003@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$btAHzc2GmogyMoKtviXGCA==$kDLMLu09lQevIZnBRW+0LzAyw4DmyYs3rR6qa/k2hKQ=', 'e0b872f378e647fb811920e985807368', 'Jasmine', 'Cruz', 'Andrada', NULL, '105202600003'),
    (3, 'arellano@legazpi.seaitt.edu.ph', 'arellano.600004@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ik9FBj8baWdnYz5zGVND4Q==$d3lc9F0FLCbUPL5VYC5KXmA0yrcMMBv3kLwvgUZ0pH4=', 'd763d42c78ab4393aed5d0ef2c87e54d', 'Miguel', 'Diaz', 'Arellano', NULL, '105202600004'),
    (3, 'balagtas@legazpi.seaitt.edu.ph', 'balagtas.600005@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$B/HdWBVmkYzvOUXAK/yAHA==$79V/M1B/Es+DEJS3e2BelfpN2yVv3KSIJI9ASX+2y3g=', '2558cd9c0c0e4b83924bde48e917c45b', 'Hannah', 'Estrada', 'Balagtas', NULL, '105202600005'),
    (3, 'barrientos@legazpi.seaitt.edu.ph', 'barrientos.600006@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Xik+JPDDU2JU35OFrQARrw==$WYEOa2BhFBixm9cftAQKsyZQXe9ieeGUpeMc73J7YC0=', '16ef04e15eee4573913bb2990433a320', 'Paolo', 'Flores', 'Barrientos', NULL, '105202600006'),
    (3, 'buenaventura@legazpi.seaitt.edu.ph', 'buenaventura.600007@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$lZT41V/ypbzCDSO1tugRzw==$p1nDaqeg61giQYJQ64if2uFRbgKUmhcJID9TPjSn75o=', '87b8e5ce7cda4a7fa45bb621a5e7d853', 'Kristine', 'Gomez', 'Buenaventura', NULL, '105202600007'),
    (3, 'cabrera@legazpi.seaitt.edu.ph', 'cabrera.600008@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$lmBH9N7fsrngOwnRUYJwTw==$r/56YKQApqKKR0Q2at1CcpiluRPiRkuynLEqsfW4nmg=', '1c966f38f6f34384869243a9510051c4', 'Joshua', 'Hernandez', 'Cabrera', NULL, '105202600008'),
    (3, 'calderon@legazpi.seaitt.edu.ph', 'calderon.600009@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$hXuKBFaSh6GMkIwizNF/Pg==$Yl4SOcdltDgcjpzA4NZByFnyFr2eGm2e6qB5LqYtits=', '2c80bf0c0c374cdeac83233910c438b6', 'Angel', 'Ibarra', 'Calderon', NULL, '105202600009'),
    (3, 'dacillo@legazpi.seaitt.edu.ph', 'dacillo.600010@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$FJPKb0oVEOkAotmAwnJ+JA==$IpYPaB/mok6/7JICV+powrMPOvTXiojs5cTii/ma7EQ=', 'a20e4ecbc73e48daa71c91e6642e0b91', 'Nathan', 'Jimenez', 'Dacillo', NULL, '105202600010'),
    (3, 'dumalag@legazpi.seaitt.edu.ph', 'dumalag.600011@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ClBN0Wj3U0TBp8rRUf8LrQ==$qxbpkTQPIZg+WCauBbR+uMRDxtrSCI/nX9sAV4w14RI=', 'd76e59aa88b84cda9a9d8bebf3257753', 'Bea', 'Katigbak', 'Dumalag', NULL, '105202600011'),
    (3, 'escalante@legazpi.seaitt.edu.ph', 'escalante.600012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$6qt6Ca2u5488kqZIUOk2pw==$eNo3AjFXpDeMns/ZxuaJ03iWCeLyb01Rwz93QTNyUVs=', '7914052405f14af8b008f411579a2e36', 'Ryan', 'Lacson', 'Escalante', NULL, '105202600012'),
    (3, 'espinosa@legazpi.seaitt.edu.ph', 'espinosa.600013@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$1+RcHeJ8v1cJPzCy2GcVNg==$qv4gebAgyk5hCfldh6DJOJdSPRU55X6X5zg4jxAP2pg=', 'eda75cf9187645b686975f5706dd5851', 'Camille', 'Manalo', 'Espinosa', NULL, '105202600013'),
    (3, 'fajardo@legazpi.seaitt.edu.ph', 'fajardo.600014@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$3KEW9P/p+QRHAnUV9H85MA==$bjJkzqlpMolX+ACKCsbAaZ+bQNaSU1ed9ybT28vKH7s=', 'a1e5ce3b50c740e997613d0faf3c7d7d', 'Daniel', 'Natividad', 'Fajardo', NULL, '105202600014'),
    (3, 'gonzales@legazpi.seaitt.edu.ph', 'gonzales.600015@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$yqSlY52Y2aufsu6V7gaXMQ==$tDDrOvzbqEZjTgtBOTolEudcBIH7Z+hfd1VlOUD501Q=', '7b5b3c4cf3a54f3c95ed85ae46769b47', 'Trisha', 'Ong', 'Gonzales', NULL, '105202600015'),
    (3, 'guevarra@legazpi.seaitt.edu.ph', 'guevarra.600016@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$d2Fq14TifsXwkeCvCSYDLg==$PYQy7EHmg2+CdxlsRAzyE8Nlj/VqFSOwKQaHOKTwYSo=', '69011584c529482e8356f9da4fcbd86f', 'Kevin', 'Pineda', 'Guevarra', NULL, '105202600016'),
    (3, 'ibanez@legazpi.seaitt.edu.ph', 'ibanez.600017@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$cB8H9vHsXFCnh9mKwcm39g==$9UKa8i7I3GPJzv/UZ9udMt9OtSD1PH7cTatR8GUHa0I=', '8ac215adf43f4b2d95b80df8acaa4a7f', 'Alyssa', 'Quizon', 'Ibanez', NULL, '105202600017'),
    (3, 'jacinto@legazpi.seaitt.edu.ph', 'jacinto.600018@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$TDI95U06VaTBjhPoTWN4QQ==$CWZ7C3NFRtNiEOBWILUIpl6lB4nvCwLoX1YkEn1Mdmg=', '63df04fa37824fb38af3ec26ccb85f5b', 'Jerome', 'Robles', 'Jacinto', NULL, '105202600018'),
    (3, 'lagman@legazpi.seaitt.edu.ph', 'lagman.600019@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$sFoYsRGQogfyGeJWPlOkIQ==$EBs3KQt+1E1vSPCd5RtxSoJ2eNZIJwW+gxtIili9QIM=', '04824eb38fa042378dc7ba7c29b0e994', 'Patricia', 'Sarmiento', 'Lagman', NULL, '105202600019');

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

INSERT INTO students (user_id, lrn, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, p.lrn, p.first_name, p.middle_name, p.last_name, 'Active', @now, @now
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
-- UPDATE users SET password_hash = 'PBKDF2-SHA256$600000$s2CL6WELWPpfTYJeOwDZTw==$ghcx4QspYAy5d1sdXShDgwat1PB2aBXl+YQMh2x00gk=', security_stamp = '550aaa2ee63643509e1c57b7c3d26247', failed_login_count = 0, lockout_end_utc = NULL
--  WHERE role_id = 1 AND email = 'admin@legazpi.seaitt.edu.ph' AND id > 0;
