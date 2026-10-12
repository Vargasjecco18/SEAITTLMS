-- Sample sign-ins for SEAITT e-LMS: 1 administrator, 1 registrar, 1 guidance counselor, 1 academic coordinator, 1 academic provider (central team), 8 teachers and 40 students.
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
    (1, 'admin@legazpi.seaitt.edu.ph', 'admin@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$tG7cMduE9YUElvvZ3y21bQ==$sHAppfN08b7WG7YSuiATHcgAJdcAAPG9ubugVo4Z+74=', 'bb78f842c1974491a34d2f2362baff36', 'Maria', 'Cristina', 'Santos', 'A-001', NULL),
    (4, 'registrar@legazpi.seaitt.edu.ph', 'registrar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ZhRFlOe1mAwPQkEgFC1iug==$S6vIao8CMwdvI3RiDziZPY1WWJ8MXN0bk3FLma1/cZk=', '089cfa5dea1946848e911b3b64461441', 'Rosa', NULL, 'Mendoza', 'R-001', NULL),
    (5, 'guidance@legazpi.seaitt.edu.ph', 'guidance@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$LSCkyh0y9VJoo2pyrgy8yg==$Hp8qJcg8ahoPjzFuPcG+dgkcNe/V0Wn4rVxiSXnnAZg=', 'ada2e7bb773f45078622e09074d278b6', 'Gloria', NULL, 'Reyes', 'G-001', NULL),
    (6, 'coordinator1@legazpi.seaitt.edu.ph', 'coordinator@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$EZ0KiK9IiszZsuDs2Ufn8A==$t5vJ2ivKhdA/leFPMynv0wUcqepq/WkEshvvFUiPMQ0=', '113c13ec5e0f4ddfa84f5b01b190705a', 'Lourdes', 'Garcia', 'Villareal', 'C-001', NULL),
    (7, 'provider1@legazpi.seaitt.edu.ph', 'provider1@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$e7w1JHjTsAV1Yo0w6oal1A==$/fTNQYNeFNSAhVIIL1HQPusdjUuj0lcKOk924JkO5iQ=', 'a03e587ff9804d85baa7be6e23cb3ded', 'Elena', 'Cruz', 'Pangilinan', 'P-001', NULL),
    (2, 'vargas@legazpi.seaitt.edu.ph', 'vargas@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$b1EIJN+Pn4hL94zotBkDyg==$63inKWfAlI6Vj3wPXy4ekeHz/NckKpxXFyiHg7sNr+U=', 'ea7581bd2418434c99fdc167fc427d03', 'Rafael', 'Mendoza', 'Vargas', 'T-013', NULL),
    (2, 'villanueva@legazpi.seaitt.edu.ph', 'ricardo.villanueva@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$u/fYxmsrHfofm9aQJPo1mQ==$cll/ZUkBpfpU6uZz05eT7j84AWUPrB2P+nGVGP93Hco=', 'dc532209d6364c2c9c1306b113307aa2', 'Ricardo', 'Dizon', 'Villanueva', 'T-001', NULL),
    (2, 'bautista@legazpi.seaitt.edu.ph', 'angelica.bautista@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Y1qEf9nHlt9OBZ+9VR5Nvg==$T/WL8GBV5BrP2n1FWuboGYAnJ/ZJM29U/x19JdWJq2E=', 'd40929f23ee848dba03fcf9e3bcd0104', 'Angelica', 'Lopez', 'Bautista', 'T-002', NULL),
    (2, 'navarro@legazpi.seaitt.edu.ph', 'eduardo.navarro@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$6tL+BxmVmsq7Ook5UA670g==$KDeMOGQOe6ogYJokylNM3sYzrtYSb7xLxTedy24/104=', '8573653293b0485baa05908fc69e7a3a', 'Eduardo', NULL, 'Navarro', 'T-003', NULL),
    (2, 'domingo@legazpi.seaitt.edu.ph', 'marites.domingo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$FzAo+3F078JZPksIakeVzw==$fi6HKVFx60ZF8siR2O0DvDci2zya/hcZH92fs+XkljA=', 'a4c31bf372a840da98fe0775049693bc', 'Marites', 'Cruz', 'Domingo', 'T-004', NULL),
    (2, 'aquino@legazpi.seaitt.edu.ph', 'jonathan.aquino@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$XMnE747Hrcq/ot75JwaAlw==$+qb7X+MxhPYjTeFO+U3TtPVnvXfyldA9bR3SQKCehFA=', 'e2eacafdc32d45b9af1392bf9d2f23d3', 'Jonathan', NULL, 'Aquino', 'T-005', NULL),
    (2, 'fernandez@legazpi.seaitt.edu.ph', 'cristina.fernandez@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$OB7uOtYigjyuJ8LXFemLyA==$RAgVMe7jX9o634mK4RuV18GL29T2qgAO3amIdCs4ifE=', 'b38acc59b4664690a5d17a725c4e58a8', 'Cristina', 'Reyes', 'Fernandez', 'T-006', NULL),
    (2, 'castillo@legazpi.seaitt.edu.ph', 'roberto.castillo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$5lvPO7Rqm7jp+zEOcx38Yw==$TVn0LSzjeOvUEfBNFUiTPzbwqVEa9A/Ui47WTQUdvYA=', 'e17b21895cda496fafe889c1c16d646d', 'Roberto', 'Garcia', 'Castillo', 'T-007', NULL),
    (3, 'delacruz@legazpi.seaitt.edu.ph', 'delacruz.789012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$TkoRK12b3Awgm9cPwlDrmg==$oY58NFfNYX/XUPoGQLY3HwXEdF5P6HKpmslOhiPn52s=', 'd91db3d7a5b14590b2fbb7003d5455ab', 'Juan', 'Santos', 'Dela Cruz', NULL, '123456789012'),
    (3, 'abad@legazpi.seaitt.edu.ph', 'abad.600001@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$OMXUKYpNPQW0V5suh+xN9w==$vF10vMTT2knZUoPjF3LjcFkueUpPlocQe0Ai4VoZ0qE=', '09700acd5ac84802a19088e6f66918b1', 'Mariel', 'Aguilar', 'Abad', NULL, '105202600001'),
    (3, 'alcantara@legazpi.seaitt.edu.ph', 'alcantara.600002@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$2xk6BRNVY85yM5Swhv34iA==$6eu9vM3oby8nDx5Lf20ttV9O1DX6PvQJ9gZ+L44xLRc=', '7fe6b0243b3f4800a5dc82508da374dc', 'Carlo', 'Bautista', 'Alcantara', NULL, '105202600002'),
    (3, 'andrada@legazpi.seaitt.edu.ph', 'andrada.600003@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$C7D/fHSzMHNuZmi04sU/YQ==$XvkIyKuERpIjNqu5IPYkaiA3j5Is2EDOmX16Hom6SHA=', '89f41de761114c03b88b8e9275e3353e', 'Jasmine', 'Cruz', 'Andrada', NULL, '105202600003'),
    (3, 'arellano@legazpi.seaitt.edu.ph', 'arellano.600004@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$E53UJBULTrNJ7fJskboF8w==$hhVgfqw02nit7sneTgrY9ODi8c6Tbyz9CstwkJZs20Q=', '4c1a8c6665b741d09a29b2934a85bbf5', 'Miguel', 'Diaz', 'Arellano', NULL, '105202600004'),
    (3, 'balagtas@legazpi.seaitt.edu.ph', 'balagtas.600005@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$hc9TQTNhSNGlx6btbqTFoQ==$hUcizX0UvObECvmuURcL4VUyGlePspZHfyePkuaT8jk=', '159f1cbb968b4ded9111a82868356850', 'Hannah', 'Estrada', 'Balagtas', NULL, '105202600005'),
    (3, 'barrientos@legazpi.seaitt.edu.ph', 'barrientos.600006@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$K/oQFtxhoQlp+0KdEVnoPw==$FXD3tGicqEJUQOhdvAPe8onaRIPnJ93/hhqrZjaCAok=', 'd69f65cc1863488285fbf14586695f4d', 'Paolo', 'Flores', 'Barrientos', NULL, '105202600006'),
    (3, 'buenaventura@legazpi.seaitt.edu.ph', 'buenaventura.600007@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$nzOctFUnm6GLMEWNE90bmw==$cObXa/sVosycMrQsPUH25jjtsQ6lvTFkB+1/pdOOGlw=', '41f19080618b41d2b2d4a2429f6c1a06', 'Kristine', 'Gomez', 'Buenaventura', NULL, '105202600007'),
    (3, 'cabrera@legazpi.seaitt.edu.ph', 'cabrera.600008@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$KcHl0VjKqV/IpjJ2GVdxmw==$pa7upiuuJSdat4BmiHDnuopnpOjJqPMOATTWNM+Q9nY=', 'bdd3386e374947ca8b75273829222355', 'Joshua', 'Hernandez', 'Cabrera', NULL, '105202600008'),
    (3, 'calderon@legazpi.seaitt.edu.ph', 'calderon.600009@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$xuCi51LI6rkT52Q9KhVaig==$pZQCPZX2Migktn7bSZAR51GHtOFwY7pK2PYGfFx+ebs=', '41dc752f40144bf8ad74d53b59200ed2', 'Angel', 'Ibarra', 'Calderon', NULL, '105202600009'),
    (3, 'dacillo@legazpi.seaitt.edu.ph', 'dacillo.600010@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$+AOTpHB12snb3r0wlh2BJg==$eeVXvOdHUPwsJaI/Kj+IHJBUp8q0t8kavRPvdK9SjwY=', '4e926924f513445e879ba6f8c4781076', 'Nathan', 'Jimenez', 'Dacillo', NULL, '105202600010'),
    (3, 'dumalag@legazpi.seaitt.edu.ph', 'dumalag.600011@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$W0vLTMlfgM1mQOsRPDlqAQ==$qaRWc9KbcnjLstHldPDFzXDMiE1ZsErxkOcg5XpDsAM=', '6f2ef896cbe04a5bb539064ffd7a22fb', 'Bea', 'Katigbak', 'Dumalag', NULL, '105202600011'),
    (3, 'escalante@legazpi.seaitt.edu.ph', 'escalante.600012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$R6/9CSGo+qbQUQcsZuPQOg==$541x6VUXY0vLPe4iBtgU7p+rMcEliHEo2TyhwWtyISw=', '143702c093e94df49393278cc3bdfa96', 'Ryan', 'Lacson', 'Escalante', NULL, '105202600012'),
    (3, 'espinosa@legazpi.seaitt.edu.ph', 'espinosa.600013@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$4C4ycC1TOPI47rPpGaGVzg==$De8GV7MrdRkL+KWz+M5dMT7FXr+ExaBgHGn04ekIfLs=', 'a163b7f0db934cbcb0d683183d844e06', 'Camille', 'Manalo', 'Espinosa', NULL, '105202600013'),
    (3, 'fajardo@legazpi.seaitt.edu.ph', 'fajardo.600014@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$T+XeXAn60BXvIZU0iXTnIw==$CbQhhrF7UnGMxtw7wapQXfNOZq+z8znzrwWROxSmh9k=', 'd2a8e830f8e540ee9d91b7cb8d42336d', 'Daniel', 'Natividad', 'Fajardo', NULL, '105202600014'),
    (3, 'gonzales@legazpi.seaitt.edu.ph', 'gonzales.600015@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$/tUKjsnrp8XO5tOGdmpc3Q==$IaP5Z2eRG9REPSjfvoPxaHvO8JOkVEoNqz/bgt0Y+nw=', '2b4ef172dad443c49a5c7ed1a99b89ea', 'Trisha', 'Ong', 'Gonzales', NULL, '105202600015'),
    (3, 'guevarra@legazpi.seaitt.edu.ph', 'guevarra.600016@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$xUVfLEmuOg8pFTwv43omvw==$glhAqMpN2ZQZpNQ7Yzv1YSC5cSy9MkYtn/ntIrAqkGY=', '96aeac28a6c54f3faedd3965d49644e3', 'Kevin', 'Pineda', 'Guevarra', NULL, '105202600016'),
    (3, 'ibanez@legazpi.seaitt.edu.ph', 'ibanez.600017@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$KlifejiquF+nkfok7sFx5w==$jhfY8CGOgncSLc8GPn1QZhVHw8Jha89BXsP2i1jBpx4=', '5c4ba541a5c04407839d3c6945080c72', 'Alyssa', 'Quizon', 'Ibanez', NULL, '105202600017'),
    (3, 'jacinto@legazpi.seaitt.edu.ph', 'jacinto.600018@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$A1w+p1Y5kbHd5CCUqZmBkg==$+PUT8yI5p+xH5XgdRki3vIwqjnprniVYfWj5Uhvvv2o=', 'd95d66c1ccf44fae8cb3d82ef312ae98', 'Jerome', 'Robles', 'Jacinto', NULL, '105202600018'),
    (3, 'lagman@legazpi.seaitt.edu.ph', 'lagman.600019@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$awAnXeAdRauWNu/3d4m2pQ==$vBL3LIVkZktXyNwQS7GFjLzOcAuwgpxSI1FWS61z090=', 'de22249c509d478799752f6819b066fc', 'Patricia', 'Sarmiento', 'Lagman', NULL, '105202600019'),
    (3, 'macaraeg@legazpi.seaitt.edu.ph', 'macaraeg.600020@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$3hg2S83p3Uspt/mhlO+PxQ==$FLZTRi6x7Nzj2w7rnaTdpBDmkqjriaXCKcfHL2f2pOc=', 'd60974b3c6f94dffb3b82987b3d7dca8', 'Christian', 'Tolentino', 'Macaraeg', NULL, '105202600020'),
    (3, 'madrigal@legazpi.seaitt.edu.ph', 'madrigal.600021@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$rqwrtIR6QYVcN8Hawu6ysA==$FwDP33xJOhXNLwB36QkbLmKRaak6CR9m9ua8XDYj64k=', '9ffb1477d60248c5a4d22673bddad223', 'Janine', 'Umali', 'Madrigal', NULL, '105202600021'),
    (3, 'magsaysay@legazpi.seaitt.edu.ph', 'magsaysay.600022@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$dv+piR3n2V452Ly8u/uGzQ==$cRHTB3yMkUq6s2pnfl4OiZgUPXggbZNvgtdRTQ7SLBw=', '75314523486d4dc98c1f4318d1e76bd2', 'Vincent', 'Valdez', 'Magsaysay', NULL, '105202600022'),
    (3, 'mercado@legazpi.seaitt.edu.ph', 'mercado.600023@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$k5Ln+sdwcjhkSwSPyYaT/Q==$+lzjdEE8W89xOikFbF7B/7FE+dtQXQDj+DeLR7RwhmU=', '03cb2df058de4e59bf60a4f0789f1a12', 'Nicole', 'Yap', 'Mercado', NULL, '105202600023'),
    (3, 'mojica@legazpi.seaitt.edu.ph', 'mojica.600024@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$gk+O7BVK9+1x9X+xBJr9EQ==$lX6OjcB64nQxKOZwLTjWaC65mCwbMc2norUbjUzkamc=', '3fec43af5ec845a3bfbbc887aff38166', 'Adrian', 'Zamora', 'Mojica', NULL, '105202600024'),
    (3, 'olivares@legazpi.seaitt.edu.ph', 'olivares.600025@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$5qjz7rsIaluLlgwk98/gNA==$DntiNlPGbygDswfdS4roHTf6PhvS08+dsqio/AK4f7g=', 'b7ca8c4552c34b38834333edc4770172', 'Samantha', 'Abella', 'Olivares', NULL, '105202600025'),
    (3, 'palma@legazpi.seaitt.edu.ph', 'palma.600026@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Q33Cfw3GkzmYRVcF8rJOUg==$uzZDnt4qn/vLP5OFsU4i+KHeJg0Cdpb8eWd2v6oiwWI=', 'c0936a4fa957416c9ac5699ce50f500a', 'Francis', 'Bernardo', 'Palma', NULL, '105202600026'),
    (3, 'quirante@legazpi.seaitt.edu.ph', 'quirante.600027@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$fSnjXdXnBHiqXv1SyRo+ew==$nbrL91ScIGVktXowhDJZv33C5/A440MKo7xhhGZPrcs=', '2b4ebe9c48574009804c82ee1cda4683', 'Louise', 'Cabral', 'Quirante', NULL, '105202600027'),
    (3, 'rafael@legazpi.seaitt.edu.ph', 'rafael.600028@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Cv6TmxaHFsBhVeFsoqh6sQ==$PfvooZffk3jQ7FRmZ+TgnquOaAq4TX+qCG8ClTLKwZE=', '8a4ce84880af4b9eb902efbfb07caf05', 'Marco', 'Delos Reyes', 'Rafael', NULL, '105202600028'),
    (3, 'sandoval@legazpi.seaitt.edu.ph', 'sandoval.600029@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$MVdef5Y6Y3CcW1sWrOBrFA==$mDzHzCESezRwonh3N+UIEKl+8Li9ppGO49oojAR9qMc=', '6c656da471c94cdc8cbf14c24e9e63ae', 'Isabel', 'Enriquez', 'Sandoval', NULL, '105202600029'),
    (3, 'tanedo@legazpi.seaitt.edu.ph', 'tanedo.600030@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$D9vn4aGDNJLPOBm4talHEg==$5BLmUX+yOKm+3rR3+OuSvCphU8ccZdIhqMlzzzmCJME=', 'bf016702b2b045dfada887dd12e4214e', 'Gerald', 'Fuentes', 'Tañedo', NULL, '105202600030'),
    (3, 'torres@legazpi.seaitt.edu.ph', 'torres.600031@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$dMuiGDkZmHTY76Pekzhkzw==$cq74HnLRAgENZQZnKhnUkT2jotxjtD/OWbb+gne+8KQ=', 'd1902d41e14c428db9d7a32660bda875', 'Danica', 'Gutierrez', 'Torres', NULL, '105202600031'),
    (3, 'urbano@legazpi.seaitt.edu.ph', 'urbano.600032@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$X2D0YysoaSfMXtBKUB+7fg==$kmhXqnXrqSDMw9QqXJCcgO4QAkV5jpJ41Muk2MAh96Q=', 'ec21bc7f208849959fc3dd52c8015a82', 'Emmanuel', 'Hizon', 'Urbano', NULL, '105202600032'),
    (3, 'vergara@legazpi.seaitt.edu.ph', 'vergara.600033@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$E1oPq5L02HIcY1i9DeNdog==$2glL0aIrP7ATPW+EQP0//igPkvmAB2EPcj+CM+NlRm4=', '769a6526a64d469ba0c5a912553a08cc', 'Rhea', 'Ignacio', 'Vergara', NULL, '105202600033'),
    (3, 'villamor@legazpi.seaitt.edu.ph', 'villamor.600034@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$IyJzRmKOi5i0z0TMkjI+Tg==$pWBHIduYWC2bosNUU1ZQUUBg1ddqi2h9i4IJK4LDuoQ=', 'b763f5eeb9be4f249e6963d617014f43', 'Lance', 'Jose', 'Villamor', NULL, '105202600034'),
    (3, 'yambao@legazpi.seaitt.edu.ph', 'yambao.600035@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$gFRzNq+edKrfd47MBPfUYQ==$ArwCFBTGyH08h0WVs+95zB+6WRjQ8OH+jg5x8PIxgyw=', '094a33a7da8e40cc9166461c8427b599', 'Maricar', 'Lim', 'Yambao', NULL, '105202600035'),
    (3, 'zafra@legazpi.seaitt.edu.ph', 'zafra.600036@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$dMYpmq37PGnwzSNQYtFvDA==$I4eoCfUyx6cvEUfSN5DtPjEbk3oo1tbjLB/dPfaicBs=', 'd2bf7a50af8443049832a920059eae87', 'Oscar', 'Moreno', 'Zafra', NULL, '105202600036'),
    (3, 'ancheta@legazpi.seaitt.edu.ph', 'ancheta.600037@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$b68mRVak3agZCDxvrPSPYg==$rIZ1g83Q3Ebq4FOPIAGiYwdnDPjK06lsawxHpCVver4=', 'e12832181a4e43f985e92c9a7d266381', 'Princess', 'Nolasco', 'Ancheta', NULL, '105202600037'),
    (3, 'bonifacio@legazpi.seaitt.edu.ph', 'bonifacio.600038@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$/n0wNbcnQ8Lon+7s4OM3xw==$qjKf0kNTCx6X0FBOex99lC+K/eWmI6fuAE4GSvnIdEM=', 'df3df92dfdfb4812a4894e57e910376c', 'Harvey', 'Ocampo', 'Bonifacio', NULL, '105202600038'),
    (3, 'corpuz@legazpi.seaitt.edu.ph', 'corpuz.600039@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$GSmU8QOhAy5LIe5sMDiXbw==$pQuG9/9v3xGSk8YVGyXTvQrpChwr9zDF/Vnq9dhFfQ4=', 'ce9f6a9f8f64479380069f4b2b211acb', 'Faith', 'Pacheco', 'Corpuz', NULL, '105202600039');

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
-- UPDATE users SET password_hash = 'PBKDF2-SHA256$600000$3OQE/X7FHXfKaRaYUvjuUA==$FBGg4+guKyAwp/vBe5w1MyZcvV1t4w6wOCHIyFswYEI=', security_stamp = 'ae7949983b7e4a0daac8fb876de5edcb', failed_login_count = 0, lockout_end_utc = NULL
--  WHERE role_id = 1 AND email = 'admin@legazpi.seaitt.edu.ph' AND id > 0;
