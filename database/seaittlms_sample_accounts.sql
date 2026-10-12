-- Sample sign-ins for SEAITT e-LMS: 1 administrator, 1 registrar, 1 guidance counselor, 2 academic coordinators, 13 teachers and 40 students.
--
-- The sign-ins are easy on purpose, to try the system: the email is the last name (or the role) + @legazpi.seaitt.edu.ph and the password is
-- Teacher1, Teacher2, ... for the teachers, Student1, Student2, ... for the students, and Registrar1, Guidance1, Coordinator1, Coordinator2, Admin123.
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
    (1, 'admin@legazpi.seaitt.edu.ph', 'admin@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$oZNGSeP4V9QeIW6M0Co/TQ==$W7URwTPfSVzzYHL9e5cvDS+XAxI4/VVO+22JAzkasn4=', 'a6e13544f7944620bbe87a600fd9502a', 'Maria', 'Cristina', 'Santos', 'A-001', NULL),
    (4, 'registrar@legazpi.seaitt.edu.ph', 'registrar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$lZc3nt2zwLyNcFCnZ2pUrw==$L0n+4VBRzb0DS1U9WKbZVqPnx2htMqIs95n7+N1PUJo=', '16f4efd199c54c6fb03df6e30e877137', 'Rosa', NULL, 'Mendoza', 'R-001', NULL),
    (5, 'guidance@legazpi.seaitt.edu.ph', 'guidance@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$uTS6T3xq8ngjK6zEwHS/MA==$zH7wWHmkuaNrZbvIhUyPUMH4ClC3Ld8M8isudVYy1ns=', 'a84b0a60a66e4919a59f69ee35700390', 'Gloria', NULL, 'Reyes', 'G-001', NULL),
    (6, 'coordinator1@legazpi.seaitt.edu.ph', 'coordinator@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$QhYbjaScG470HoqGF/wMhQ==$qcTjhn6zpWnNN0CenSBJOwNDjTMRLTRjirnglOX2WXo=', '56f00513935543338bf0b723329052a8', 'Lourdes', 'Garcia', 'Villareal', 'C-001', NULL),
    (6, 'coordinator2@legazpi.seaitt.edu.ph', 'coordinator2@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$XLsZX6HdgG/mmeDDjZ6WFQ==$UX9M+3U0p7mnoRq2c/vmAkPtD3i1h7n+BRR1rpUfNr0=', '3acf27429a66421dbed9363e7c4a9543', 'Benjamin', NULL, 'Soriano', 'C-002', NULL),
    (2, 'vargas@legazpi.seaitt.edu.ph', 'vargas@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$PS0pOfzziiu47qWI8TdgCg==$U8CsUMIIsk3r3Ouc/czxEA494JRXdYrLH5dH7QL+XEc=', '0a2f8cea7e4f4cc1807616be705b1155', 'Rafael', 'Mendoza', 'Vargas', 'T-013', NULL),
    (2, 'villanueva@legazpi.seaitt.edu.ph', 'ricardo.villanueva@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$4PPi6xCcYl61qyYbjH145g==$yxFDE6soS5vazj647yol68HYi4jpADsKcHlwFfu03mc=', '2b7ab41bc7f84ed08dd6eb11c4b3b3f2', 'Ricardo', 'Dizon', 'Villanueva', 'T-001', NULL),
    (2, 'bautista@legazpi.seaitt.edu.ph', 'angelica.bautista@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$BjRu/kx5tjdLaOhUJ06tgA==$SZvKVwpeCuRJTDXEZxbDMc9PEyriYqRaWcrDK+H6Hak=', 'd37bf895885040368f462b916becd396', 'Angelica', 'Lopez', 'Bautista', 'T-002', NULL),
    (2, 'navarro@legazpi.seaitt.edu.ph', 'eduardo.navarro@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$lA0Ip4RnoguBhr0rFO00mA==$O+qaVgJuUkdOLd+n30rYs0bHIlq2B2mHCuMe2nftW5M=', '19f2ce2e09074dfd98db1980ea493743', 'Eduardo', NULL, 'Navarro', 'T-003', NULL),
    (2, 'domingo@legazpi.seaitt.edu.ph', 'marites.domingo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$pKyoWJO4GH9JTme++PKx5Q==$R3kJVwmaCayfkes0Z86KGu/JwoYw4mVBYcBQDLNJZcQ=', '09b6358b094a466a9f2ac606612bdb58', 'Marites', 'Cruz', 'Domingo', 'T-004', NULL),
    (2, 'aquino@legazpi.seaitt.edu.ph', 'jonathan.aquino@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$yoOpb+rSgPAlaDfJHGk+9g==$C5nrQ3D2hCefuuYVDrd+mL31RAG1YZpVP2loQLeOySY=', 'ce1beaaeb73a4aaaa6a5062ec9de3577', 'Jonathan', NULL, 'Aquino', 'T-005', NULL),
    (2, 'fernandez@legazpi.seaitt.edu.ph', 'cristina.fernandez@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$/WFVB9r+rLlyYOmc3ukA0g==$pI7pgXF0LShIZqn0YrEyNgJIdRbxGDcArE137XeHmek=', '2b806cdd60574939a6ee831bb701f879', 'Cristina', 'Reyes', 'Fernandez', 'T-006', NULL),
    (2, 'castillo@legazpi.seaitt.edu.ph', 'roberto.castillo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$6H4pZsjZ103lq53ITqNjzw==$goUQ5CQY88iDRfIRXoMcTAm2JKSgNkIkhFQGZVYwcHo=', '268cfc6060884bba85c04a41d8420b52', 'Roberto', 'Garcia', 'Castillo', 'T-007', NULL),
    (2, 'ramos@legazpi.seaitt.edu.ph', 'liza.ramos@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$SM+mQbaeft2rR0fU4zmUmw==$Kb3u4w1QOJr9dkohTafNzVd3bL3RsoZoYpfQ6iAByLo=', '631283ab4b85408f96c81c7763da53cd', 'Liza', NULL, 'Ramos', 'T-008', NULL),
    (2, 'salazar@legazpi.seaitt.edu.ph', 'mark.salazar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$LOFS4n+CmGQ3alsDYRuWSA==$x1ogUshVDaUS+wzZ1Gvlq3kmUjmgXSNfCzMZkz2zbI8=', 'a9d0d72614344a62a3dcb738d68d0126', 'Mark Anthony', 'Perez', 'Salazar', 'T-009', NULL),
    (2, 'pascual@legazpi.seaitt.edu.ph', 'teresita.pascual@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$5H3+W8A9M4hgXGrciQQWSQ==$kNFm5bAVhu7GrZXXYPbv7nEFzTHRsN3H8zYeWVwCsp0=', '0ea158dac58e47c39b9a2014e557f3be', 'Teresita', NULL, 'Pascual', 'T-010', NULL),
    (2, 'ocampo@legazpi.seaitt.edu.ph', 'gabriel.ocampo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$y8t91vWZRa3I9wu8gMzgoQ==$R92XhLXsbuH9xjHDtUJLvkX6cTYtTzUIHOUSXypKB4E=', '45a0dec53a904815b168cbfac989a3bb', 'Gabriel', 'Tan', 'Ocampo', 'T-011', NULL),
    (2, 'magtanggol@legazpi.seaitt.edu.ph', 'shiela.magtanggol@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$oajd0uzZm4ZRH+UtRKXOXQ==$lGHaXml2cjlR3ctbzbkSnJK+gjFyvFDLVP76tqS6T3E=', '104646eb72984de2b2bf888243a975f1', 'Shiela Marie', 'Uy', 'Magtanggol', 'T-012', NULL),
    (3, 'delacruz@legazpi.seaitt.edu.ph', 'delacruz.789012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$CPxfuRoXeebpoDYMuX/7+g==$RRv9wDuOQQp87uHrMeSd4lNcrc1XfY8FrxFuCAArCzg=', '37c661e927fa497ca0327921e7083f28', 'Juan', 'Santos', 'Dela Cruz', NULL, '123456789012'),
    (3, 'abad@legazpi.seaitt.edu.ph', 'abad.600001@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$KLVOfara2s+mCnrdC8YRsg==$AyakK7cg3gtGfIkvxLLHvxme8+z9hzfaxJck6hFkggU=', '6c20f9bbc72d494a9a32f067cf6331c0', 'Mariel', 'Aguilar', 'Abad', NULL, '105202600001'),
    (3, 'alcantara@legazpi.seaitt.edu.ph', 'alcantara.600002@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$TK7y9u5JrKsqO80vu8jQdw==$JTxicPCF385b94TtlrB7vgt3BBmT6mQ1KAi2XofaMG4=', '332752a9afae433a920d9300063bff65', 'Carlo', 'Bautista', 'Alcantara', NULL, '105202600002'),
    (3, 'andrada@legazpi.seaitt.edu.ph', 'andrada.600003@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$PTlcAG+0Td/I72IZQ0hRJw==$fBXbwnIynI9VKPQ7PIel/axOtNDYhPoAeWs7KKL3+oc=', 'e62b48c1e9214aa5b426cb3706a86fbd', 'Jasmine', 'Cruz', 'Andrada', NULL, '105202600003'),
    (3, 'arellano@legazpi.seaitt.edu.ph', 'arellano.600004@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$jW8XUJBs2Div8FZDLXk5nQ==$wpcePt2pmMXJ3ls2mSqbHZU6JYRUPxhUlNGCOQWg+ic=', 'eca6312c6d624db79b222c813835d6e0', 'Miguel', 'Diaz', 'Arellano', NULL, '105202600004'),
    (3, 'balagtas@legazpi.seaitt.edu.ph', 'balagtas.600005@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$pd4hnWp4tjzRNqS0un3mZQ==$hrTEfnb3ZOSRB75MNeOsAyoJKfhp4xJEBLa9rKg3iZQ=', 'cdd5ef35cd9e44f8b03e326542c302c7', 'Hannah', 'Estrada', 'Balagtas', NULL, '105202600005'),
    (3, 'barrientos@legazpi.seaitt.edu.ph', 'barrientos.600006@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$kBRMFsMnLj43R4EpehmeAw==$snZAIpcFvKw2TdLkES9mOJdKyg2H9XeXwa5cG7hYkMU=', 'edddf5bfd7e945f2ad5c45bd2f5b3008', 'Paolo', 'Flores', 'Barrientos', NULL, '105202600006'),
    (3, 'buenaventura@legazpi.seaitt.edu.ph', 'buenaventura.600007@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$bzr490I42HMG4wJ+cQivyw==$mL8amG33Ia/eyux2fcv1N0fbmvr+jRG8Pc3vM5ZpZPc=', '7d67e54da3e1442bba96b1dab62c68f4', 'Kristine', 'Gomez', 'Buenaventura', NULL, '105202600007'),
    (3, 'cabrera@legazpi.seaitt.edu.ph', 'cabrera.600008@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$3fcDOlgL0I/6RKZtepdWYw==$JAvBDgntakoYb61AOfFG28r1vp8DlBal6Kg1fEHkxZ8=', 'e090f899ac7347faad9e2fa371dd21a0', 'Joshua', 'Hernandez', 'Cabrera', NULL, '105202600008'),
    (3, 'calderon@legazpi.seaitt.edu.ph', 'calderon.600009@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$+uZ7TWh9SPTkd/urkYBisw==$QsS38SUkOb/hKkgzw9moAdE4AjNNUscVkj94gnmSNPU=', 'ff9b5e70e30049fc9cfb15df069afdfa', 'Angel', 'Ibarra', 'Calderon', NULL, '105202600009'),
    (3, 'dacillo@legazpi.seaitt.edu.ph', 'dacillo.600010@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$jPGSvmw1H3WalA8FLWreAQ==$Ta8rFcZ5QrAunyQpEZANiT5Uzvo5+kjrOWRqk0l3OqE=', '823dccd4884b451c95cea6bf0ee13bb9', 'Nathan', 'Jimenez', 'Dacillo', NULL, '105202600010'),
    (3, 'dumalag@legazpi.seaitt.edu.ph', 'dumalag.600011@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$oyCH8LuOZ1g5d2AxmoT50A==$i5PuwGjSA3AV0HtRLPmSbHX8Cr7pMZOPrQ/ZT7gwRNQ=', '8b211e38092446f39a9361ef7ce41259', 'Bea', 'Katigbak', 'Dumalag', NULL, '105202600011'),
    (3, 'escalante@legazpi.seaitt.edu.ph', 'escalante.600012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$GVATOOIq7gOWO92rlBXDUQ==$vsK1dnT4pmb225dgTCZU4U6vAXNiz5QHNyOuZoqAb4c=', 'f951f1858287457ea2080131d5719495', 'Ryan', 'Lacson', 'Escalante', NULL, '105202600012'),
    (3, 'espinosa@legazpi.seaitt.edu.ph', 'espinosa.600013@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$drXhc8niDfBapIlRZMZywg==$Owj/Bp6cAJus4L5s2ZYrm4ccjXchD9jCDSQUxmL7Yhg=', '66ee34de6e384667a2eac671c93da9a1', 'Camille', 'Manalo', 'Espinosa', NULL, '105202600013'),
    (3, 'fajardo@legazpi.seaitt.edu.ph', 'fajardo.600014@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$4jxbDiKmoCO67rzp5/C+2Q==$GH5L5drl5lyHhCEeglBntj3zaoadbfoT7t8ZLJG1TVM=', '9a0d4547607346408a2e15734b7184d0', 'Daniel', 'Natividad', 'Fajardo', NULL, '105202600014'),
    (3, 'gonzales@legazpi.seaitt.edu.ph', 'gonzales.600015@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ec5+iz3+pGXW89MaS/jNeQ==$9xHjO3uHZ3AdPC1FDu7hvNfAeWHqKr7Vi956N+JRQmY=', '4449ab982af443f797e569052dcab5b4', 'Trisha', 'Ong', 'Gonzales', NULL, '105202600015'),
    (3, 'guevarra@legazpi.seaitt.edu.ph', 'guevarra.600016@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ZMNASrkW8pnBAZmzNmI7aA==$qwcHSzI6HnBEViL5TarDT9DVuAueoZF0gKAwb8oaA+0=', '251c16863bc0461cad470aa5bdf65117', 'Kevin', 'Pineda', 'Guevarra', NULL, '105202600016'),
    (3, 'ibanez@legazpi.seaitt.edu.ph', 'ibanez.600017@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$0LQUTbU4MCjzdt1C8tcHlA==$6aCG5qmNby3wC+Wi5Q1ilkJkn5vG01e49Z5MfxJOADc=', '2ba95a21d8614642bc9481634c701f7e', 'Alyssa', 'Quizon', 'Ibanez', NULL, '105202600017'),
    (3, 'jacinto@legazpi.seaitt.edu.ph', 'jacinto.600018@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$7xPZwlsRnv3bq0LODG6tNw==$1U8C2Xgp8ga/2P2j/ddeuPDldeaoH3TFdshD9ZkUg2s=', 'e84729876bf446aab6e9a021b064d4f8', 'Jerome', 'Robles', 'Jacinto', NULL, '105202600018'),
    (3, 'lagman@legazpi.seaitt.edu.ph', 'lagman.600019@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$LpQGXcl7IhlkXQzYvPkfsg==$FxCTXpopwhACck7bEWYJWRSPdfRAH02rKi68vT4+I0Y=', '14814d0a7df7467f83f4c70723020732', 'Patricia', 'Sarmiento', 'Lagman', NULL, '105202600019'),
    (3, 'macaraeg@legazpi.seaitt.edu.ph', 'macaraeg.600020@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$4orXZkRpw/a2Kk/5ZsvMGg==$Wsy2bbxK/0bLvRiWRt7A/XOiE4OtDF7LMa2N54kRBvE=', '435f5282f49941309fa780b587a26cc3', 'Christian', 'Tolentino', 'Macaraeg', NULL, '105202600020'),
    (3, 'madrigal@legazpi.seaitt.edu.ph', 'madrigal.600021@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$60rKGv54jdEnuA47l/CDcg==$Wq17kZovlMArgH08bW0qkSsCMGTj0BpRLmgR1uqieZk=', 'd9fb9034147045e5b7cc172cda70e87e', 'Janine', 'Umali', 'Madrigal', NULL, '105202600021'),
    (3, 'magsaysay@legazpi.seaitt.edu.ph', 'magsaysay.600022@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$E6axpDjWz6IzQ52pIsaTdg==$nZBbj3mRfP5uak1jw4rHjwHTuXAm6IZpE9JgRswJPb4=', 'f6cc8837cf8c48caabd1d2b2262a9264', 'Vincent', 'Valdez', 'Magsaysay', NULL, '105202600022'),
    (3, 'mercado@legazpi.seaitt.edu.ph', 'mercado.600023@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$J8yVM0v2UsDym8/e/935Zg==$HfiGJAzvntfQabd87xzFL9NjYNHN+Y/LEYqPfccx2FY=', '425754e5901b4e2f976a6222c7f57ef3', 'Nicole', 'Yap', 'Mercado', NULL, '105202600023'),
    (3, 'mojica@legazpi.seaitt.edu.ph', 'mojica.600024@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$rT3Ayu+TXJpXQzYZYlBdQA==$l2a+sU3aX+V4LYBqUOxno5L/WvoPkz41mxz81h2/fuE=', 'a613604fde4f41fd93391ad9ad95205d', 'Adrian', 'Zamora', 'Mojica', NULL, '105202600024'),
    (3, 'olivares@legazpi.seaitt.edu.ph', 'olivares.600025@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$jOboam/FJqM6OTRXgxKsaw==$YXpMCb7WfFuuxHG0/WI0HgqVns6iaLy5bDDxJGnozsw=', '37e808cc5e8a4120a936c371b572bba9', 'Samantha', 'Abella', 'Olivares', NULL, '105202600025'),
    (3, 'palma@legazpi.seaitt.edu.ph', 'palma.600026@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$b4IQG5MNPHiEK0Im4Il/sw==$qnDnspMoYSc6WnczHWTCWPpdU2VsyfdhbHSshmWGyBE=', 'f2344cca1e5f4123aa9eae197229c1e5', 'Francis', 'Bernardo', 'Palma', NULL, '105202600026'),
    (3, 'quirante@legazpi.seaitt.edu.ph', 'quirante.600027@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$6QX86iZx1BiUh4HyhTTCvw==$hoS6kCg/mCmy/GjmDH0pAOJwwJSNoEYMLi2MnuUqORU=', '3520c0dbb3504a9b86cfcbafbbc0586a', 'Louise', 'Cabral', 'Quirante', NULL, '105202600027'),
    (3, 'rafael@legazpi.seaitt.edu.ph', 'rafael.600028@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$BqGf2QOMTp1OsXJLHq/HTQ==$mUexRA8tAW/FgkXpadDNdvsw8ggkjzBDRYT75XI1quc=', 'fd54424258e845e6b1c7f02e1e845e46', 'Marco', 'Delos Reyes', 'Rafael', NULL, '105202600028'),
    (3, 'sandoval@legazpi.seaitt.edu.ph', 'sandoval.600029@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$yJzVCe6VCczCeGVcUwCeAw==$RpmNeyaFRC0QyLEA2eJzGNp+v/qJL7M6p/tj6isqeCk=', 'adea395ddf5a4e77b8ff0b1d863b2c69', 'Isabel', 'Enriquez', 'Sandoval', NULL, '105202600029'),
    (3, 'tanedo@legazpi.seaitt.edu.ph', 'tanedo.600030@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$uMefBvZrPu8L4lWD7AIilA==$f4JYh3xIxE4vAydFPnDWYmrR6l97czfA2MCciNjKLMA=', '032a441dffca47f986d09319ef411225', 'Gerald', 'Fuentes', 'Tañedo', NULL, '105202600030'),
    (3, 'torres@legazpi.seaitt.edu.ph', 'torres.600031@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$JSB4NS5CVdE52pQb/XOU8A==$6ePB4F9NHNWvvsWwCDyGQSIVcqjhd++Mi4rg7yTe2oY=', '640df4c86cd842868427951491aee410', 'Danica', 'Gutierrez', 'Torres', NULL, '105202600031'),
    (3, 'urbano@legazpi.seaitt.edu.ph', 'urbano.600032@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Cim0GCiJiHczllni1nPf2A==$aFbBJogjX369aIPkUCTi0EmepuX9XuwVjzbS3doC6ss=', 'bf402c9f781c498ebbad45fb11ef683b', 'Emmanuel', 'Hizon', 'Urbano', NULL, '105202600032'),
    (3, 'vergara@legazpi.seaitt.edu.ph', 'vergara.600033@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Bnl24yVkk++W2ZGZp4Jftg==$Dt9rQK/3nyOdf8dQ79fNj3XrDAF8kFKjBq5+ImBGwoQ=', 'ee8861e839fa499e974187cb0afb67d2', 'Rhea', 'Ignacio', 'Vergara', NULL, '105202600033'),
    (3, 'villamor@legazpi.seaitt.edu.ph', 'villamor.600034@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$vz+SGnxDy0wuYS+9y4SxdQ==$v3GDzAAlAg9PQQy+FarWpRmkx1O9ynYISepv1+mLy5g=', '25b43655b46b4622bb9f57a539c8d80b', 'Lance', 'Jose', 'Villamor', NULL, '105202600034'),
    (3, 'yambao@legazpi.seaitt.edu.ph', 'yambao.600035@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$WcU1F7FuQDOqv2d4EGmfsg==$Ui2TrnedxBbD+JGjOq51POLr5o+atbxegb+CxPDn8tc=', '67fc7ec9f25e43f180b6fb81549f267c', 'Maricar', 'Lim', 'Yambao', NULL, '105202600035'),
    (3, 'zafra@legazpi.seaitt.edu.ph', 'zafra.600036@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$uL50A8wfWXmDUCc5x5ytqw==$ZjxARCs7umg16zUC1LZew7u6qvmkAAw7QAzjLxD9aKE=', '71784c8361b74fff847bff4ffe447202', 'Oscar', 'Moreno', 'Zafra', NULL, '105202600036'),
    (3, 'ancheta@legazpi.seaitt.edu.ph', 'ancheta.600037@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$GDGdPnRt3VmxinIk7ds5lA==$cPmj1sGs4E3eFjConMOgV2NVV80zyvgHBCZiFB6JOXQ=', 'c22e252153e64612bdb6af44b05b4dbb', 'Princess', 'Nolasco', 'Ancheta', NULL, '105202600037'),
    (3, 'bonifacio@legazpi.seaitt.edu.ph', 'bonifacio.600038@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$9xXAJLzsz91D5JP87CjIfw==$EwfJL2Cts9jghPVYH3PdR8wjPzWJbmOXkmMjlTKR7cg=', '1f26b9957bcd482da2b169fe5e499109', 'Harvey', 'Ocampo', 'Bonifacio', NULL, '105202600038'),
    (3, 'corpuz@legazpi.seaitt.edu.ph', 'corpuz.600039@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$YuK1hflTLO8Q0genOJYXvA==$OJ2LBO7mFTtoG3sb7EwFYD1Bll04K3ibfCgWBJNCI18=', 'fae22a3cabd44834bd4e1ae9848fa05c', 'Faith', 'Pacheco', 'Corpuz', NULL, '105202600039');

-- 1) The accounts that are not there yet. Role 1 = administrator, 2 = teacher, 3 = student, 4 = registrar, 5 = guidance counselor, 6 = academic coordinator.
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
-- UPDATE users SET password_hash = 'PBKDF2-SHA256$600000$ptt7WPGhuNUpYWio9yFGzQ==$i8e7hWi1ZFOg06ok5eQn7oh6yIf7yN26r+rGK1v45RY=', security_stamp = 'c8e9b3c7747142af9263e8eb8ecbeb8c', failed_login_count = 0, lockout_end_utc = NULL
--  WHERE role_id = 1 AND email = 'admin@legazpi.seaitt.edu.ph' AND id > 0;
