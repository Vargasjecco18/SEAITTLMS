-- Sample sign-ins for SEAITT e-LMS: 1 administrator, 1 registrar, 1 guidance counselor, 2 academic coordinators, 2 academic providers (central team), 13 teachers and 40 students.
--
-- The sign-ins are easy on purpose, to try the system: the email is the last name (or the role) + @legazpi.seaitt.edu.ph and the password is
-- Teacher1, Teacher2, ... for the teachers, Student1, Student2, ... for the students, and Registrar1, Guidance1, Coordinator1, Coordinator2, Provider1, Provider2, Admin123.
-- For example: vargas@legazpi.seaitt.edu.ph with the password Teacher1. (The list with every name, email and password is kept out of this file.)
-- Give real people a harder password: an administrator can set a new one in Users Management > Edit.
--
-- How to run it: open this file in MySQL Workbench (File > Open SQL Script...), then press the lightning bolt (Execute).
-- The tables must exist already (start the app once, or run seaittlms_reset_and_rebuild.sql first).
-- It is safe to run again, and it is safe to run over the sample accounts of an earlier version of this file:
--   * a person who is not there yet is added;
--   * a person who is there already (with the email of this file or of the earlier one) gets the email and the password of this file, and keeps
--     everything else (courses, enrollment, grades). The sign-in of a locked account is opened.
-- The school has exactly ONE administrator, ONE registrar and ONE guidance counselor, so for those roles nothing is added when the role already
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
    (1, 'admin@legazpi.seaitt.edu.ph', 'admin@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$T/brLDHgLfcf200JQQ7EKA==$LesVomCkvhHHS5bs2Ya1/dihZBDiSjx1aWMurMTAFlk=', 'e21eb14d27144e138f6a1e9abec7243a', 'Maria', 'Cristina', 'Santos', 'A-001', NULL),
    (4, 'registrar@legazpi.seaitt.edu.ph', 'registrar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$j17YpbxRP5t2tP4XN85u+Q==$gSYHCJugipe6cAhwT/DSjHrNmj86oWSH1DfwNonC8Bw=', '6173a641e83f4884a2b86ecb2886107f', 'Rosa', NULL, 'Mendoza', 'R-001', NULL),
    (5, 'guidance@legazpi.seaitt.edu.ph', 'guidance@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$yg9QaIAgdTEA49YX+nUsqw==$mJe7d3Uy/7oyh0DXd+CrYcmjXwIIzrEPjdu6fq8FOkk=', 'b389955d26944b74b9abf1354bcf2038', 'Gloria', NULL, 'Reyes', 'G-001', NULL),
    (6, 'coordinator1@legazpi.seaitt.edu.ph', 'coordinator@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$gFJgLsz564HmaH9RHRTBqQ==$GAayDffTBSPvfnwTfbilzyXGluAYjyz2Bp7ESe/h23M=', '785353e1e3484e9f847a0117e0c4b14e', 'Lourdes', 'Garcia', 'Villareal', 'C-001', NULL),
    (6, 'coordinator2@legazpi.seaitt.edu.ph', 'coordinator2@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$qUnTeo78JXlgfqfkjAGLIg==$IGFZoyDwRdQR1uRPSx5vmUFfhjXRZbCEuSRh/r/udU0=', 'f296c9665d644d7e81e9ed29e3078597', 'Benjamin', NULL, 'Soriano', 'C-002', NULL),
    (7, 'provider1@legazpi.seaitt.edu.ph', 'provider1@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$flSEDGXyp10CkDE4nXPzIg==$+s0xaRCVrFz9FnuBYkOYIQkXw2o+29OpBLgkamJF9AM=', 'ed5aff9605dc4b1c918d06f17d637c48', 'Elena', 'Cruz', 'Pangilinan', 'P-001', NULL),
    (7, 'provider2@legazpi.seaitt.edu.ph', 'provider2@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$boZmLxi+bZaCvA5jR00T0A==$KqQo/3ZDn9QaERmLFa0CjbwJEmUMw6Ql1pHvX9GgKDc=', '17b4a484f56f4fd8b35cc6f77e88a0d1', 'Dominic', NULL, 'Rivera', 'P-002', NULL),
    (2, 'vargas@legazpi.seaitt.edu.ph', 'vargas@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$7ooUGwWLU9/+BhOqe/JrlA==$zVdi1wNZfseVPRbIus+we/joiqpdTn5lLflyIMHe5z0=', '8cf77529cc974e98a7a258a1ec9130b7', 'Rafael', 'Mendoza', 'Vargas', 'T-013', NULL),
    (2, 'villanueva@legazpi.seaitt.edu.ph', 'ricardo.villanueva@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$2PKQre63pAsOjpUR0/lZFw==$4J3GygjC13sOpp2x3IxhLp616TFyLY0/WELgkxhqLQg=', 'f2b7dd62bb3f41318ac06dc5137eed9e', 'Ricardo', 'Dizon', 'Villanueva', 'T-001', NULL),
    (2, 'bautista@legazpi.seaitt.edu.ph', 'angelica.bautista@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$yreEAkV1YZypn4RCuCZFBA==$SY9h88BUOoGYiUskiLcEEVH7fHEpOsNVoUxDm/BrouM=', '42a0c15fd40142088653390a07aec26e', 'Angelica', 'Lopez', 'Bautista', 'T-002', NULL),
    (2, 'navarro@legazpi.seaitt.edu.ph', 'eduardo.navarro@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$NXTmxbeU0yV7Wu3xoP8eGg==$H3j2K+snMhYf/6eT6Dj8cM7LmFHIZdZXBIm18SnyQnc=', '18a306ff33ba4be292a9db4e9a35fc8e', 'Eduardo', NULL, 'Navarro', 'T-003', NULL),
    (2, 'domingo@legazpi.seaitt.edu.ph', 'marites.domingo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$uAUOcQcb+x4v/nyFMtM27g==$pxhD78pYNK4vOC+n2/wTO43aJI/jnL5qAbYlt80UMlw=', 'fa69cc60a37f489181e2c806c9141130', 'Marites', 'Cruz', 'Domingo', 'T-004', NULL),
    (2, 'aquino@legazpi.seaitt.edu.ph', 'jonathan.aquino@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$E1sWMGCitRsnOO/eaiC/Iw==$+/e6ybgaufaOV4Ui5P22SzMVX8eFcAUwV4JYHT17jko=', '44ea9a679d0b47e4b9b9bb9cb4da42d1', 'Jonathan', NULL, 'Aquino', 'T-005', NULL),
    (2, 'fernandez@legazpi.seaitt.edu.ph', 'cristina.fernandez@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$3NjUXXFp5xeErxTBgEiB1A==$g1qozCkTvb5qzvDL0WNsG85k9yv18jT+htAe8SguU4c=', '762d682a23a34d63a3f42b737c16c792', 'Cristina', 'Reyes', 'Fernandez', 'T-006', NULL),
    (2, 'castillo@legazpi.seaitt.edu.ph', 'roberto.castillo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$uhxWdW6dGq8eHWpcE6DbfQ==$vs1VrSLGqGwyk08DV6xULAOHecc8KFKW4lGtv7kPckI=', 'cec738e9f3824f6c8673ef3208e125e6', 'Roberto', 'Garcia', 'Castillo', 'T-007', NULL),
    (2, 'ramos@legazpi.seaitt.edu.ph', 'liza.ramos@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$zGDtg3w2G6I5Ab8rTnN5/A==$bLn9misGWYzUbmwhG6LuuV5JPTZN336Pc4RC55ehdR4=', 'ccf63fdad436494d834574e31a0abf9f', 'Liza', NULL, 'Ramos', 'T-008', NULL),
    (2, 'salazar@legazpi.seaitt.edu.ph', 'mark.salazar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$iSIkop99YU1v8fuiKjJJJg==$ns1ZGbvwfl1tyrb0VYTcTp3eQ+oulFvf53XeWuCk/YA=', 'd4e1a3fba85241a78e9d21eec567a94f', 'Mark Anthony', 'Perez', 'Salazar', 'T-009', NULL),
    (2, 'pascual@legazpi.seaitt.edu.ph', 'teresita.pascual@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$YnBLpTEqpybhi3/qaQO0wA==$ixVWyDgymZ270eHQvrbRjqO4VNyA28LMoa1+bV34DfY=', '811d21dbfc9542b190d94bb76a502efe', 'Teresita', NULL, 'Pascual', 'T-010', NULL),
    (2, 'ocampo@legazpi.seaitt.edu.ph', 'gabriel.ocampo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$PsUKPlXWZAli17IwuGv9yA==$NGxucx8jK5EUouROqO6xPcQ51nn7COZ7E6jbf6c+o6Y=', '86d559de648f4af09c75bf2e32b24bd0', 'Gabriel', 'Tan', 'Ocampo', 'T-011', NULL),
    (2, 'magtanggol@legazpi.seaitt.edu.ph', 'shiela.magtanggol@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$4Jod3hl9cb2q9VQzorj32g==$weIOz7846TDGH6gBf2m5iCSP/JYUEAsPm6ZGjChUX1k=', '20a8cfc8791147a2a3a03f61d74cddca', 'Shiela Marie', 'Uy', 'Magtanggol', 'T-012', NULL),
    (3, 'delacruz@legazpi.seaitt.edu.ph', 'delacruz.789012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$VdmlzRGkThzhVh4PwsJAzw==$EJlL8VR+NbyFQUaDbduWPCvdSSyA6VrcblGlA+oxTsY=', '208b790e48fc41478df75c52f226f01f', 'Juan', 'Santos', 'Dela Cruz', NULL, '123456789012'),
    (3, 'abad@legazpi.seaitt.edu.ph', 'abad.600001@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$oFB6PBuolw6wyTJlnsH5hg==$Ua1fk7LR2D1ZrbqrwWMGQo894VVgFGDbNql5bVP7Xm4=', '97106a0df1164215906d7fda27644daf', 'Mariel', 'Aguilar', 'Abad', NULL, '105202600001'),
    (3, 'alcantara@legazpi.seaitt.edu.ph', 'alcantara.600002@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$xgeDNcPZfqx/Pm6hl3f1cw==$sau47x0x6uTA0EBE8oQMzgZIcQQQNWEkZ0/ok1NMvHw=', '2f0299fab01d49dea93319561e1dc699', 'Carlo', 'Bautista', 'Alcantara', NULL, '105202600002'),
    (3, 'andrada@legazpi.seaitt.edu.ph', 'andrada.600003@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$6vqoj/5LORb0/g97kWsy3Q==$u7eFuuYj7pkKEHOD+ZAaRbCC6Kt5frZjsh+e3ZCdQv4=', '098aa7e6299444dcaff23992ff15af01', 'Jasmine', 'Cruz', 'Andrada', NULL, '105202600003'),
    (3, 'arellano@legazpi.seaitt.edu.ph', 'arellano.600004@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$FUpzaWBd8iHojuleteRqqQ==$DWvNwGHtzBBxG4a1No8PhWMlB64G+ZVhxfclbC+rESQ=', '9ea4d6fa46f545c2a7060b24b56464b7', 'Miguel', 'Diaz', 'Arellano', NULL, '105202600004'),
    (3, 'balagtas@legazpi.seaitt.edu.ph', 'balagtas.600005@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ih+N5WljNc3zixVxFABnvw==$8b+9VjYMR/ejv9IhHZQSBYo6fywgOCpM/sursZ2Q9Bc=', '92c6c087de9541d4a2a7a38997d3181b', 'Hannah', 'Estrada', 'Balagtas', NULL, '105202600005'),
    (3, 'barrientos@legazpi.seaitt.edu.ph', 'barrientos.600006@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$9QA0gBKWR4P0gxYaL3yYZA==$9f+Ye5Jfr2SczVwyEgSwTODcnJe1dWr6mOmzLHsDicM=', '3efb0b601fb14b3bbe687509209f1b23', 'Paolo', 'Flores', 'Barrientos', NULL, '105202600006'),
    (3, 'buenaventura@legazpi.seaitt.edu.ph', 'buenaventura.600007@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$N6NKx+LixNUXAx0CLl0/Lw==$vzSS6w6a03gMu1dURAc6sv/G5osi7ruPP40leLuTRpY=', '7b53dd2b0cb64e8994822f060e2ca1c0', 'Kristine', 'Gomez', 'Buenaventura', NULL, '105202600007'),
    (3, 'cabrera@legazpi.seaitt.edu.ph', 'cabrera.600008@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$XXKktGLGvy1NzvE8Bo0AjQ==$MY1xYsvjD/vnqykYpBRraindxn2/GlgJrFSsP732FDM=', '0c979e3fb839441ab0804e3b6e13ee53', 'Joshua', 'Hernandez', 'Cabrera', NULL, '105202600008'),
    (3, 'calderon@legazpi.seaitt.edu.ph', 'calderon.600009@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$HGMnwCwRGrTtyaVDOdpHzA==$EdC/QvwcymIlvgFp06UKiBLWVs20h27MAawUOjVmIp0=', '4d049949e1814ca08b1930442617c576', 'Angel', 'Ibarra', 'Calderon', NULL, '105202600009'),
    (3, 'dacillo@legazpi.seaitt.edu.ph', 'dacillo.600010@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Gwk4tqNdYdD1wqkuLYOaUA==$nb6zXlExrBuc6t0VAW3EmBINyFlaAivMk0VVG7yqkzg=', 'f0491bcb0ac94f38ab2eabebe8b43d6e', 'Nathan', 'Jimenez', 'Dacillo', NULL, '105202600010'),
    (3, 'dumalag@legazpi.seaitt.edu.ph', 'dumalag.600011@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$C6DHL1ju5K/ixYt/HBzHJg==$IYpYmfbiRLVcoHZg/i4hPSZtGqDMnjNVtfkfhwJEwgE=', 'a44157dad54b45a79bc8b3b48d03e530', 'Bea', 'Katigbak', 'Dumalag', NULL, '105202600011'),
    (3, 'escalante@legazpi.seaitt.edu.ph', 'escalante.600012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$61C3M5noBiHifwffze09og==$xns6ghlq+1qUpOUWpPGedzdp7mEc+96PV3QddUAk6rI=', '802fe362b23b4fac9e60f11d8d19cf41', 'Ryan', 'Lacson', 'Escalante', NULL, '105202600012'),
    (3, 'espinosa@legazpi.seaitt.edu.ph', 'espinosa.600013@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$yCKK7omgsU0ZLOMPY7iFEQ==$fS8StuGgq/NJG9WV/Z3VKwnzqUyLTwDsnSnl4yxiMys=', 'f640a3a808ed46eeb8a47be99ccfabf4', 'Camille', 'Manalo', 'Espinosa', NULL, '105202600013'),
    (3, 'fajardo@legazpi.seaitt.edu.ph', 'fajardo.600014@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$XbGZcylkfpFzNxodYm0ngA==$TxdRF1cMfjcH2A1rBixp+G7F5fGQWe6GOgKqCG4wyxk=', '72abf7d6045646148a0a77dc0c175e64', 'Daniel', 'Natividad', 'Fajardo', NULL, '105202600014'),
    (3, 'gonzales@legazpi.seaitt.edu.ph', 'gonzales.600015@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$VWBtSwq4nNBqKWmOBI5wvA==$x5By7wQm/DoCCRHKtEEMM0U4B4+2EbqNAv6vnfBk/qA=', '9af2cba9736c4f6faaf680109a51d468', 'Trisha', 'Ong', 'Gonzales', NULL, '105202600015'),
    (3, 'guevarra@legazpi.seaitt.edu.ph', 'guevarra.600016@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$StgjnN8DwtDdSOc8J35tng==$+uaTEj5dLycOflsVSlV/z8pwjLCt1WwLckMTE5CQzss=', 'dfd6336398654722ba9a6e6bf3045ec6', 'Kevin', 'Pineda', 'Guevarra', NULL, '105202600016'),
    (3, 'ibanez@legazpi.seaitt.edu.ph', 'ibanez.600017@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$X4a5KGig1kPTJ9abBPEi3g==$GHaBbfJqnqLczN7gVz0Q8YKbWysxVe/Z08TSJGptwtA=', 'a7a077647d2947f088cde91cf2d004b1', 'Alyssa', 'Quizon', 'Ibanez', NULL, '105202600017'),
    (3, 'jacinto@legazpi.seaitt.edu.ph', 'jacinto.600018@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$XOK7HgveLkT0K2T0YxdVPw==$GZoW79ZFfaRmDBEO2NUqqjFfQsWKJT9asSYcjHRiIe0=', '46ad4ace6d7844aba8f168d907f53fe3', 'Jerome', 'Robles', 'Jacinto', NULL, '105202600018'),
    (3, 'lagman@legazpi.seaitt.edu.ph', 'lagman.600019@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$fweBraYWZmjA1eiLX18wsw==$M0982zGZNayx14TU99IS36+0mF8IEebWEz79VVXo+yo=', 'ae7317339a4a441688a074108b5d6cb3', 'Patricia', 'Sarmiento', 'Lagman', NULL, '105202600019'),
    (3, 'macaraeg@legazpi.seaitt.edu.ph', 'macaraeg.600020@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$FjF8Vp7EPnr/0K+s0oMVRQ==$JSV54lrLGUylsPrAjRP/zSGzAEEYR5WykKXKLOZEU6I=', 'f42db89f6bad4fad9d9c5ed811e83a13', 'Christian', 'Tolentino', 'Macaraeg', NULL, '105202600020'),
    (3, 'madrigal@legazpi.seaitt.edu.ph', 'madrigal.600021@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$MGMgOFwk/4y+6k8FqFLdXQ==$6AgN8vlmIEONIIefYotRWG4s0zOaVOJY6DZA0jg92XA=', '962573369fa541b4972709a95ec4c4ec', 'Janine', 'Umali', 'Madrigal', NULL, '105202600021'),
    (3, 'magsaysay@legazpi.seaitt.edu.ph', 'magsaysay.600022@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$qoymgzjHX1UaYb0WxeRu2g==$b4spESvnU5NfWlIN0jWR1Xgpr+Ow27ag34W/n17pga4=', '314526346e3f43ea87ec14b8a8fc067a', 'Vincent', 'Valdez', 'Magsaysay', NULL, '105202600022'),
    (3, 'mercado@legazpi.seaitt.edu.ph', 'mercado.600023@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$yxY1qIAr33yIp02+At2xig==$+AtcLUcA82Ut+pHTekyR5grmDUaAcVGKpSaCyWetyH8=', '6c5db6fe8c304092825858f325958735', 'Nicole', 'Yap', 'Mercado', NULL, '105202600023'),
    (3, 'mojica@legazpi.seaitt.edu.ph', 'mojica.600024@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$JVKDJHMUSa7dzepSk6tvAw==$Ipe+4a+Genpbb0aXzy9dfCDmjAGy10XeqvePT4JMvUI=', '77f60bf070e543db8895b69475baba23', 'Adrian', 'Zamora', 'Mojica', NULL, '105202600024'),
    (3, 'olivares@legazpi.seaitt.edu.ph', 'olivares.600025@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$vDXyw8zqgF1N7C5FMYzPoA==$TFZ02Aug4KmmSDGpb+k/bGZO1gh+hynQNQuHhaxobHM=', '2eadde57434842a991d4d281875f810c', 'Samantha', 'Abella', 'Olivares', NULL, '105202600025'),
    (3, 'palma@legazpi.seaitt.edu.ph', 'palma.600026@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$rmTNbNaNGHs6MRY5BtDSIw==$SCo212btzoAZhfr2yFgNGHD/qKogkyKnnOqK8vBWqh4=', '85f1860a5e7a4e54a24f015965bf7eb8', 'Francis', 'Bernardo', 'Palma', NULL, '105202600026'),
    (3, 'quirante@legazpi.seaitt.edu.ph', 'quirante.600027@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$NKRlGMGn2O2WHpayzD9fYA==$XMC3F7N6OFKoJ2hvKpH4Hq/aRsvOZ7WFk6Sqqx7rQGk=', 'ea16116cefbc477899817e8c2e2e9e0a', 'Louise', 'Cabral', 'Quirante', NULL, '105202600027'),
    (3, 'rafael@legazpi.seaitt.edu.ph', 'rafael.600028@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$iWGYbVGzkKbQ1Io4Ubwapg==$qaOFZc4iiiY7ghS0IKpf9KZt81xT3j+bJ7QXJ/4CWQw=', '8a1ab5461852444197f4e65f5cb00ab3', 'Marco', 'Delos Reyes', 'Rafael', NULL, '105202600028'),
    (3, 'sandoval@legazpi.seaitt.edu.ph', 'sandoval.600029@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Jc5m3iKHFMzdadNxxuKv6A==$su24d3uO9IFPpnZw9GSeXfRScwB7atac31s7Rhmk7Js=', '3de634ba46284853a423eff02bcf4eff', 'Isabel', 'Enriquez', 'Sandoval', NULL, '105202600029'),
    (3, 'tanedo@legazpi.seaitt.edu.ph', 'tanedo.600030@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$D62RBhMaxhqbiEY3Zea+mg==$biwlNjJxiRpmRnL+TOIjflsYSAe5k8XouqK0SfieH9I=', 'd2b6d27234114bb5974d508729f7a02b', 'Gerald', 'Fuentes', 'Tañedo', NULL, '105202600030'),
    (3, 'torres@legazpi.seaitt.edu.ph', 'torres.600031@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$5x0Mdk4QZqXL02dPRi+qwg==$91z6noZ9lSd2A7mNg3pN4W1xJfGekGfP//j1t0KTixc=', '5dee1b8f00e341d28d1ab59b560c6b78', 'Danica', 'Gutierrez', 'Torres', NULL, '105202600031'),
    (3, 'urbano@legazpi.seaitt.edu.ph', 'urbano.600032@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$z+XdXcXrgiyIV3Q23xhzfA==$Nt0ZXebvnDTMscM4Ze02WG0xFjTybOyyMNMfLjfyTc0=', 'b14f263f2af740b1a5a5a52a98c624ef', 'Emmanuel', 'Hizon', 'Urbano', NULL, '105202600032'),
    (3, 'vergara@legazpi.seaitt.edu.ph', 'vergara.600033@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$OZMuikgOXJ1D65wUuPLa6w==$SO9/13deqhwvd/oPoe36t3ty1F/JOyZbKCxegiJawWA=', '837194e2d41645fa83572bc2bd5209d6', 'Rhea', 'Ignacio', 'Vergara', NULL, '105202600033'),
    (3, 'villamor@legazpi.seaitt.edu.ph', 'villamor.600034@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$xA7Z58726cYzTMpGTnjLJA==$tz7w1PRfgsFWQCsCFJeG5njb/eYQIbrDho3mNyV0fOk=', '80223cb508e94f88be553ccd2c391fe0', 'Lance', 'Jose', 'Villamor', NULL, '105202600034'),
    (3, 'yambao@legazpi.seaitt.edu.ph', 'yambao.600035@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$sL+h5Wi7rJiLydFMwE0aGw==$/MesAsH+Ic1VExnbY5d+ABtJ3NFFbO97xfzODp8fRe4=', '448c59149bf242dca768917d4df91c18', 'Maricar', 'Lim', 'Yambao', NULL, '105202600035'),
    (3, 'zafra@legazpi.seaitt.edu.ph', 'zafra.600036@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$IUgCDJwN/1+RgbloDur3vQ==$pqmzw2pX/Vq6jyLG8ejx44HkMKWOIZRjQBbb/xCZULM=', 'ee0d79e632204a459f2b05282fe82296', 'Oscar', 'Moreno', 'Zafra', NULL, '105202600036'),
    (3, 'ancheta@legazpi.seaitt.edu.ph', 'ancheta.600037@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$UUPZYFR62WBKK8Cvd6TX6g==$luZgtLnVANhYnq6hnqt3w0RqZgoAkonoENB52eUswLI=', '0a39e5a69ad740e9bdf2376bd7b2603a', 'Princess', 'Nolasco', 'Ancheta', NULL, '105202600037'),
    (3, 'bonifacio@legazpi.seaitt.edu.ph', 'bonifacio.600038@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$cc8AFijR76/D2DUgS4l4dw==$wIuvHc2cBrc05rFvCjVwbTzxsknH5TbSHLYPBZmYoXA=', '9f1fba1ae5d541fd90ebc8d95ee0591e', 'Harvey', 'Ocampo', 'Bonifacio', NULL, '105202600038'),
    (3, 'corpuz@legazpi.seaitt.edu.ph', 'corpuz.600039@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$6Dq5kkVZ+rxaPtEYmaf6RA==$MzI+KpvlZo4+UB/d6Yfy/L5rR49dTSI4NOagPWh3hgQ=', '6b2a44d5640d46189f74ff71ee329b34', 'Faith', 'Pacheco', 'Corpuz', NULL, '105202600039');

-- 1) The accounts that are not there yet. Role 1 = administrator, 2 = teacher, 3 = student, 4 = registrar, 5 = guidance counselor, 6 = academic coordinator, 7 = academic provider.
INSERT INTO users (email, password_hash, phone_number, role_id, is_active, security_stamp, failed_login_count, created_at_utc, updated_at_utc)
SELECT p.email, p.password_hash, NULL, p.role_id, 1, p.security_stamp, 0, @now, @now
FROM sample_people p
WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u.email = p.email OR u.email = p.old_email)
  AND (p.role_id NOT IN (1, 4, 5) OR NOT EXISTS (SELECT 1 FROM users u WHERE u.role_id = p.role_id))
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
-- UPDATE users SET password_hash = 'PBKDF2-SHA256$600000$l0HhqcSc4+1Ybpxn2kIbbg==$6zl2I3eBtp82LP0ALgOKDwQesk0PxwroUHl0QZIPH34=', security_stamp = 'd6792aa2fc754441a17fa38a38a1a254', failed_login_count = 0, lockout_end_utc = NULL
--  WHERE role_id = 1 AND email = 'admin@legazpi.seaitt.edu.ph' AND id > 0;
