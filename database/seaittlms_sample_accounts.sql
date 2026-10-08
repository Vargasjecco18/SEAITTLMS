-- Sample sign-ins for SEAITT e-LMS: 1 administrator, 1 registrar, 1 guidance counselor, 2 academic coordinators, 12 teachers and 40 students.
--
-- How to run it: open this file in MySQL Workbench (File > Open SQL Script...), then press the lightning bolt (Execute).
-- The tables must exist already (start the app once, or run seaittlms_reset_and_rebuild.sql first).
-- It is safe to run again: a person is only added when they are not there yet. The school has exactly ONE administrator, ONE
-- registrar and ONE guidance counselor, so for those roles nothing is added when the role already has an account
-- (the app makes the first administrator by itself from the Seed:Admin settings in appsettings.Local.json). The school can have
-- several academic coordinators.
--
-- The teachers and the students are plain accounts. They have no courses or enrollments yet: the administrator gives a teacher
-- courses (Users Management > Teachers > Courses), and the registrar enrolls the students (Enrollments, type the LRN: the account
-- is found). Until then the students are listed under Not enrolled (a card of the administrator's dashboard).
-- A student signs in with the last name and the end of the LRN, like the ones the registrar's enrollment makes.
--
-- The passwords are not in this file, only their hashes (the same kind the app writes). Give the passwords to the people
-- and ask them to change them (an administrator can set a new one in Users Management > Edit).

USE seaittlms;

SET @now = UTC_TIMESTAMP(6);

-- The people to add, in a temporary table that disappears when the connection closes. It has no character set of its own,
-- so its text columns get the database's, the same as the real tables they are compared with.
DROP TEMPORARY TABLE IF EXISTS sample_people;
CREATE TEMPORARY TABLE sample_people (
    role_id INT NOT NULL, email VARCHAR(254) NOT NULL, password_hash VARCHAR(255) NOT NULL, security_stamp VARCHAR(64) NOT NULL,
    first_name VARCHAR(100) NOT NULL, middle_name VARCHAR(100) NULL, last_name VARCHAR(100) NOT NULL, employee_number VARCHAR(30) NULL, lrn CHAR(12) NULL
);

INSERT INTO sample_people (role_id, email, password_hash, security_stamp, first_name, middle_name, last_name, employee_number, lrn) VALUES
    (1, 'admin@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$pH9g5G7aCxPOeSuNxwZHaQ==$JnYEQnooNaNuv1yAQB6pT2KwOCWj3t9zAYAgXZbDR7w=', '0a0b3a3846e94ff099ac1dad4316cf39', 'Maria', 'Cristina', 'Santos', 'A-001', NULL),
    (4, 'registrar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$bp4RJYbf1AjTi8+ZzvCofQ==$2STLqu7SdhVq30Bkn9RcZx2Vy7J3phHix3MCy6AsYXg=', '8c346ebffea642128c9d4d37c44be1f4', 'Rosa', NULL, 'Mendoza', 'R-001', NULL),
    (5, 'guidance@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$z+C3vMkFlVZMBFmQDMuDvQ==$ehRxZwMDOJEKI5pasQ7V8XdnbHbVrABGT27JB9WSpcU=', '913d7a48c2dd452fb474e9dc49f83ace', 'Gloria', NULL, 'Reyes', 'G-001', NULL),
    (6, 'coordinator@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$TDdcp0JSEfJzw6I5LGjQYA==$CXQyXSSYE+oGtlJBEnTvE+xY3VCLsn3bk9rYzmliZL0=', 'f05233add25c499797ee622bbe6e4ed4', 'Lourdes', 'Garcia', 'Villareal', 'C-001', NULL),
    (6, 'coordinator2@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Gr7Vdba3qE92on5sB0HgHQ==$VmZasRKjeOTnUhSfZwU0Lojn+k0Nfvo9PfNjSmvDd90=', 'b00247600a1c48bd951c448a6dde8a4c', 'Benjamin', NULL, 'Soriano', 'C-002', NULL),
    (2, 'ricardo.villanueva@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$DMVowzFnhePS03fMa/koWQ==$yWgpZcw8CBtccdHmG3DTVNZHyPNiIqSTqVT2ZglYzlQ=', '1aa8999d36ce44ac88bcc429d47a186d', 'Ricardo', 'Dizon', 'Villanueva', 'T-001', NULL),
    (2, 'angelica.bautista@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$nu42OXraZQ/GA1vh2lxYtw==$//vYTPuaS3aSeoQLPm/di5Dy18AWvo8cQ/36YexZpX4=', 'b9646f81f2c84350a50093f55004c651', 'Angelica', 'Lopez', 'Bautista', 'T-002', NULL),
    (2, 'eduardo.navarro@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$wLiY0z1XsGUbNqAiNUJPxg==$ggCpwozfHL7Bg/EQv7XHFsGlJqjmhddP/3gi5hE3rJY=', '5227101f40754a7199f368fa40304473', 'Eduardo', NULL, 'Navarro', 'T-003', NULL),
    (2, 'marites.domingo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$E1iI2Ysg0qjqYW7yuFkIdA==$SpjZBbdsnqk1A6fUoICjdabWgATTK6FefN+LZNtK3iA=', 'ae5ea294157d4e739d12d1c1392490bc', 'Marites', 'Cruz', 'Domingo', 'T-004', NULL),
    (2, 'jonathan.aquino@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$53SScNzr5wL2tLsiG9B3eQ==$ARdTdifhb/CniBg0ebh83OC/eKIY3uhLe7FFX+oFrn4=', 'f8c2479c7d7345d3835c05e327562e17', 'Jonathan', NULL, 'Aquino', 'T-005', NULL),
    (2, 'cristina.fernandez@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$2ySBny931ZRRt7cWfUBzZQ==$KRwg9FcDqfQmWDWVPU+pt1h/iwx8l9lthRnRBvhj/FU=', '4470203af54d465bb6b99f8247f10e36', 'Cristina', 'Reyes', 'Fernandez', 'T-006', NULL),
    (2, 'roberto.castillo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$U1dCUrZOpch0dHFLsyD/0Q==$0pSHFhkuWI2p7vGCRNTuNx4PxE/MimWsSA8yC5/bB2E=', '0484abc2c2b54267a88327f05d5790de', 'Roberto', 'Garcia', 'Castillo', 'T-007', NULL),
    (2, 'liza.ramos@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ulidfKEsNp7GL352qV5Yxg==$fDEF9FG6X8+fk2ojhfZpYhFn2wtmJoUoGpFJR1GnzDg=', '2ba97175bb5f48628234fb2cc628b435', 'Liza', NULL, 'Ramos', 'T-008', NULL),
    (2, 'mark.salazar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$KXgV+XNk/izKIM857U7ENw==$w2bfaJYQJLvWWma9OZ/7gHpI/Lla97r3K83bNKWqPg8=', '6c775bbe16ae465fa8b7e378ccc485c3', 'Mark Anthony', 'Perez', 'Salazar', 'T-009', NULL),
    (2, 'teresita.pascual@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$eZLrYWrpKJSEFYn120L9Xg==$k5PXyifRVvYMwZjvwZ4j6NEXO+1SQ7sGhqwsv7UjIC4=', 'd0f5f060f34a46588a7332b11ba7b864', 'Teresita', NULL, 'Pascual', 'T-010', NULL),
    (2, 'gabriel.ocampo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$5mrtg2uTGg1RUlZXxLeVYg==$iNAW5azEd7JUATzBLJTCsS1n1R+Kg/duRTIxa4+6qWY=', 'fe3b487c9d3c4903b2eebbc07f86e796', 'Gabriel', 'Tan', 'Ocampo', 'T-011', NULL),
    (2, 'shiela.magtanggol@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$dv36WjqYjofcXpHJWzH6Gg==$FIHrN58/KOh0zQ45DRohAGreTeHw2tW7tHVYp7WOSm4=', '8fcf302cb3784dc4b7f26e2331b8e2ea', 'Shiela Marie', 'Uy', 'Magtanggol', 'T-012', NULL),
    (3, 'delacruz.789012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$h4/+N7/Uqez7GfBkR8c7mQ==$uAn3oeWjAb+W9bmY+IAAZUTd49T1Aka6m/JMQRRBiXs=', 'd470c9a600d84af19f60e1dae9572cd4', 'Juan', 'Santos', 'Dela Cruz', NULL, '123456789012'),
    (3, 'abad.600001@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$zjzcfxRR1dWISDm67Rtf0Q==$9uvnc5v5ZZDR3YrhKFrlG/AesS7bTE9boUgqvznXhV4=', 'b7e21784f0674b9998db14ec213f075d', 'Mariel', 'Aguilar', 'Abad', NULL, '105202600001'),
    (3, 'alcantara.600002@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Sm0RHiM3GNSObFyfRmycWQ==$ZutagdOA2ijWRV5HDgUQ5Rh87jcFh89/KKC+uoklu5s=', '181a71eaf7284eb9ad3222d4d6b981d6', 'Carlo', 'Bautista', 'Alcantara', NULL, '105202600002'),
    (3, 'andrada.600003@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$erQRvmqMtt5sJ+U/vtkyyQ==$X4KPyM8cP5TfPg/P/fvpbop1KSqpIaf3LV1bN+XllJo=', '5eaadd5ae37f415085851874357139cf', 'Jasmine', 'Cruz', 'Andrada', NULL, '105202600003'),
    (3, 'arellano.600004@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Nx0oz30x1e6W2YAZctfwnQ==$nHE1BwDrF9Sz66ioi/3ttlqjJqVJbbu5yb79lizivCg=', '82eb708b61e54df39c60a4ae75aec7a7', 'Miguel', 'Diaz', 'Arellano', NULL, '105202600004'),
    (3, 'balagtas.600005@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$8LNpV/utqKDCaR472ZHK2A==$wBH9XzQRnvAcDZRZFjxdPU8tpXzX1pSj12w1rMzeITE=', '9de364eba4604f179c61f958b1b3ea88', 'Hannah', 'Estrada', 'Balagtas', NULL, '105202600005'),
    (3, 'barrientos.600006@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$GotWaW8v7sA4zDojWKbP0A==$LklsL0SvoeP0FDgzMZyVkmMGKZO+eFmI5diEGwf20mc=', 'b019170cdad743a59bcd74ca4a7422dd', 'Paolo', 'Flores', 'Barrientos', NULL, '105202600006'),
    (3, 'buenaventura.600007@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$9LMosAWK0m5XT+fLfeQ5sg==$pr+s/EAIIHMDXE87xKvT0njqZnK/EuhHu9SZ7tyhZiY=', '07a8cb43e0464fdeb802eeeb6f972976', 'Kristine', 'Gomez', 'Buenaventura', NULL, '105202600007'),
    (3, 'cabrera.600008@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$TGi01r56O+6YtKmDOOTR2w==$1KhFUxYrzRsGohRZAxlvgd5rWUDlFEtceFloWkhAkPo=', '8f3cb6240fa9457681e7fd29cda0aad3', 'Joshua', 'Hernandez', 'Cabrera', NULL, '105202600008'),
    (3, 'calderon.600009@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$APYpAExo+yD0FQDE+Vwjbg==$R7nqOcxX22fksPPrP3VuWV7Nh7G9XO8aKlHv65E2US8=', '9291444f91e049dc841b592a59b152f9', 'Angel', 'Ibarra', 'Calderon', NULL, '105202600009'),
    (3, 'dacillo.600010@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$KewHVaEOplXbjCjEfXp0mQ==$C+igPYPKgTFtmwFbqkr+DbtQpTUAVHezHoYDX2w0aNw=', '1d7a3b200a634ff8a89061e1692db5bc', 'Nathan', 'Jimenez', 'Dacillo', NULL, '105202600010'),
    (3, 'dumalag.600011@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$6xbqYN4Eocscy3J3ztHWpg==$cLL3M8DXHak7RzoMXlSDQMrz0ugq56TTSGQt/N+lgqE=', 'e8472da5516b4be2bb1beb3ca59a100e', 'Bea', 'Katigbak', 'Dumalag', NULL, '105202600011'),
    (3, 'escalante.600012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$MIH+zziUp0mYElAeJGva2A==$zOQteOJEU2rjKcFKpo56qJ8BfsFyj34d2zdVWYjBoe0=', '41914fb86f864c1bb5ac126960059b86', 'Ryan', 'Lacson', 'Escalante', NULL, '105202600012'),
    (3, 'espinosa.600013@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ti9fRnMp0OGJMiLBFY8SKQ==$pf+S1znZ4+Kr89hS0nqeq3qwcB96KlW+GSkcEeXktPk=', '168fd70c03b5455db2b8e5b5602c7174', 'Camille', 'Manalo', 'Espinosa', NULL, '105202600013'),
    (3, 'fajardo.600014@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$IX/Hzs9nFtd+lMuJ8WZ4Qg==$v/xNMwZrCFv6rVfHHrRTUwzkxArN/jCd214yuvdRnEI=', 'd8c598005a8d42929a15fcb12aa07602', 'Daniel', 'Natividad', 'Fajardo', NULL, '105202600014'),
    (3, 'gonzales.600015@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$jzjCMBCZjXO6YRkW7IBNSQ==$zoTY205i0b6UmXCU+8HtVmSl1wC0mOqTFNd2shTdvXg=', '52ce467628da4c9f8844746a4c2b463b', 'Trisha', 'Ong', 'Gonzales', NULL, '105202600015'),
    (3, 'guevarra.600016@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$cTsFRZODaU6skI4jz1m4Ag==$LoIBVB/910eOuMcU6a7tuhYyuHI1JCK0K/wHweccRVE=', 'f5c9c89fe99b49eb90f110a1d94d0bcb', 'Kevin', 'Pineda', 'Guevarra', NULL, '105202600016'),
    (3, 'ibanez.600017@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$OrGZcjp+vHR41q3hbrjCqw==$vPRwGx+AuHPlrz588qEfoYkE0pZdIVNEZs9Oa3eyTqQ=', '505ed627efeb4265afbe4a808c22b604', 'Alyssa', 'Quizon', 'Ibanez', NULL, '105202600017'),
    (3, 'jacinto.600018@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$55Ae/OTX9dL5coVEl3yNMQ==$znMMvUzVK7puXNrQzrYwQ2421ET1YBOoGklofAwGdBs=', 'ceddb52d0f5c4a0ea095b9284f3c7f1b', 'Jerome', 'Robles', 'Jacinto', NULL, '105202600018'),
    (3, 'lagman.600019@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$rju0bBPdfaLDZpqG5hvvsg==$Y61+v8xqQKPgduktAvoAgXt5MmeKPZIV5Ska76WWc0Y=', '9939e106b64348b4b7a1cf424fc15c4b', 'Patricia', 'Sarmiento', 'Lagman', NULL, '105202600019'),
    (3, 'macaraeg.600020@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$luRtLevPQ/WG+d0/mYp4cg==$0x/DKa9nHEnApG4FF0Mv8r/ckbtSDfkqtSekRQbXWWo=', '64a82edfc4c943f4b4775740f4265e31', 'Christian', 'Tolentino', 'Macaraeg', NULL, '105202600020'),
    (3, 'madrigal.600021@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$0vyIuY5AswrKfS2Fts19+Q==$PsiVPA8G2qDC91Q/2uyFuuHNcKyx3zGJW8k3c58jWqE=', 'a8ca5fa1dff5484eacd767198877f507', 'Janine', 'Umali', 'Madrigal', NULL, '105202600021'),
    (3, 'magsaysay.600022@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$M8tBHRmEVP7Qa/RCsNWBmw==$McPGKzhVG9FHvB+CQxo7qLsJrp4sqU7LTPYZpN5PM50=', 'b57f9349890e4dcabd4b0feaedb7b6ac', 'Vincent', 'Valdez', 'Magsaysay', NULL, '105202600022'),
    (3, 'mercado.600023@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$iK+vEE0c827LiaOtEQOcBw==$W9eaUv2QQZUGQ8JhRIOJKiaNn2Ud+L87xvEg3QxBfNc=', '6d8b9b04b1be47bbac09195fdc3f7632', 'Nicole', 'Yap', 'Mercado', NULL, '105202600023'),
    (3, 'mojica.600024@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$3FKYEmQPjLz5iCsczSzVMA==$vfts+srIZ7Lec/bnN4G+2FMB1seYpdmKxxosBfuGoE4=', 'cf26bedaf2fb460cb25ad083dd4ed838', 'Adrian', 'Zamora', 'Mojica', NULL, '105202600024'),
    (3, 'olivares.600025@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$q/jx/4r9qBQQm0aB3S3iqg==$uHXZ6w9mPPP8XrDUIK4Xi2OrZ1p6kdt30QG5rjIGIZ4=', 'e29b1e5cfe5c45ffa9c2b37b5ed17100', 'Samantha', 'Abella', 'Olivares', NULL, '105202600025'),
    (3, 'palma.600026@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$/vXjwHPkDAmKcfrgXpQjyA==$PJkh7EiFgl9RS2ofKM01T94WY3a0wPLCasXuiRGVVjw=', '25cd48634b494574aec68d3af0fd87d9', 'Francis', 'Bernardo', 'Palma', NULL, '105202600026'),
    (3, 'quirante.600027@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Clfm7b1246AALKgBMypOOg==$fw7ad44W24sGpic47P74ZRrVZvSQzcwn8B4MYzg1LNQ=', '99f64e2047f54b50a5dc2d4003cb1ae5', 'Louise', 'Cabral', 'Quirante', NULL, '105202600027'),
    (3, 'rafael.600028@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$6fudT29PVoZV/uVPrZSCTA==$b5obiuIPyoIGYBOojTzzKNfPAPQ5e5TkOQYJRkw82Ro=', '5548427975b14eeb8c8a36d2c15ddbcf', 'Marco', 'Delos Reyes', 'Rafael', NULL, '105202600028'),
    (3, 'sandoval.600029@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$8FXZlmatQ15WMDVhDBpoSA==$Gec2n0ftqIyWmNUcCW/OS1+2Ac6MCIg7md2F676ve8U=', '39d951cd84ef4404a0aa028dac3b4c35', 'Isabel', 'Enriquez', 'Sandoval', NULL, '105202600029'),
    (3, 'tanedo.600030@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$rr8HNIY7Q9lNp8zX3s4uqA==$RinzGbfRosqN730ICyQIjumqjjOMHnYODKSMuJMYhVg=', '18d28fedb6fd4b8b8b614c5c5bc770b5', 'Gerald', 'Fuentes', 'Tañedo', NULL, '105202600030'),
    (3, 'torres.600031@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$NU+LUudulWu4k2gzsCqp1g==$FgjPyVCApBVjfO3sLU79xF1ZEOQ7aoNjETbNu8YzKww=', 'a4efc5b38f7c4f7ba6a88c06b5a73d39', 'Danica', 'Gutierrez', 'Torres', NULL, '105202600031'),
    (3, 'urbano.600032@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$MAf3hfkNDziPcGxF/XNC8w==$/2U/YGCnRdLR/BatmqPRhkT55JtKtd/McHNC5zTUtb8=', '0f68bfc240104cf09f82e2b1517f1c00', 'Emmanuel', 'Hizon', 'Urbano', NULL, '105202600032'),
    (3, 'vergara.600033@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$exgKzxq93KtbcsBzrlA/TA==$JBDdQGu6LMu9jySz/HQIpfgB4ZGPqK14VVRx1XIvSQY=', '3f828938b24f43b78501e400001c4e0c', 'Rhea', 'Ignacio', 'Vergara', NULL, '105202600033'),
    (3, 'villamor.600034@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Sv6bQxw/mwN2Kc+enpq0mg==$Tgx/isz0uBlnbhipy5lQtMfC14zvjkUtCqat5qsmnLI=', 'e02621d913e14f39a70dd95da01b79c1', 'Lance', 'Jose', 'Villamor', NULL, '105202600034'),
    (3, 'yambao.600035@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$uulJEW/RKKHZMHTnbGxeHg==$6DXttbqSdniNQDcIqN3mF+Bo3PHhSbmE7HaFQZaWEoY=', '249976b955ad43159f76027be8dd2c41', 'Maricar', 'Lim', 'Yambao', NULL, '105202600035'),
    (3, 'zafra.600036@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$mfIte0GkpOTi88uJz6bFbA==$YO2uvmbCAIT6ToAlP6rFVBnW2z8dmv3k3vnP5H12u7I=', 'cdb65bef98604fe1b2fe2800f24e3f9b', 'Oscar', 'Moreno', 'Zafra', NULL, '105202600036'),
    (3, 'ancheta.600037@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$2wGCenIcj2NMKSSjGVOMWg==$U7ycg3gFewAinhJ2lDfWiK3qWydQfL0KU/e98FQX0rk=', '1106a8940771498c9fdf688198e61fa2', 'Princess', 'Nolasco', 'Ancheta', NULL, '105202600037'),
    (3, 'bonifacio.600038@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$/0ycyi+Dm0ErgdNcKC5EyQ==$y+K1ZsQBprL2+/ww+oIJcr1PF/EssN7grcc8Bi+Ci6o=', '582ac77c2b5d4eacb84914600b53eae0', 'Harvey', 'Ocampo', 'Bonifacio', NULL, '105202600038'),
    (3, 'corpuz.600039@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$oUNN+Xpbb4D6IubnGeNC2g==$Qc5tVWOR4nXQzbewvIviaxATdrALcXMFhPlhRLUBD/U=', '9812ea5bc46f49fdb740b1e5a370caf7', 'Faith', 'Pacheco', 'Corpuz', NULL, '105202600039');

-- 1) The accounts. Role 1 = administrator, 2 = teacher, 3 = student, 4 = registrar, 5 = guidance counselor, 6 = academic coordinator.
INSERT INTO users (email, password_hash, phone_number, role_id, is_active, security_stamp, failed_login_count, created_at_utc, updated_at_utc)
SELECT p.email, p.password_hash, NULL, p.role_id, 1, p.security_stamp, 0, @now, @now
FROM sample_people p
WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u.email = p.email)
  AND (p.role_id NOT IN (1, 4, 5) OR NOT EXISTS (SELECT 1 FROM users u WHERE u.role_id = p.role_id))
  AND (p.lrn IS NULL OR NOT EXISTS (SELECT 1 FROM students s WHERE s.lrn = p.lrn));

-- 2) The profile of each account: the row with the name, in the table of its role.
INSERT INTO administrators (user_id, employee_number, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, p.employee_number, p.first_name, p.middle_name, p.last_name, 'Active', @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 1 AND NOT EXISTS (SELECT 1 FROM administrators x WHERE x.user_id = u.id);

INSERT INTO registrars (user_id, employee_number, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, p.employee_number, p.first_name, p.middle_name, p.last_name, 'Active', @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 4 AND NOT EXISTS (SELECT 1 FROM registrars x WHERE x.user_id = u.id);

INSERT INTO guidance_counselors (user_id, employee_number, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, p.employee_number, p.first_name, p.middle_name, p.last_name, 'Active', @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 5 AND NOT EXISTS (SELECT 1 FROM guidance_counselors x WHERE x.user_id = u.id);

INSERT INTO academic_coordinators (user_id, employee_number, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, p.employee_number, p.first_name, p.middle_name, p.last_name, 'Active', @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 6 AND NOT EXISTS (SELECT 1 FROM academic_coordinators x WHERE x.user_id = u.id);

INSERT INTO teachers (user_id, employee_number, first_name, middle_name, last_name, created_at_utc, updated_at_utc)
SELECT u.id, p.employee_number, p.first_name, p.middle_name, p.last_name, @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 2 AND NOT EXISTS (SELECT 1 FROM teachers x WHERE x.user_id = u.id);

INSERT INTO students (user_id, lrn, first_name, middle_name, last_name, status, created_at_utc, updated_at_utc)
SELECT u.id, p.lrn, p.first_name, p.middle_name, p.last_name, 'Active', @now, @now
FROM sample_people p JOIN users u ON u.email = p.email AND u.role_id = p.role_id
WHERE p.role_id = 3 AND NOT EXISTS (SELECT 1 FROM students x WHERE x.user_id = u.id);

DROP TEMPORARY TABLE sample_people;

-- What is in the database now: how many accounts each role has.
SELECT r.name AS role, COUNT(*) AS accounts FROM users u JOIN roles r ON r.id = u.role_id GROUP BY r.id, r.name ORDER BY r.id;

-- And the people who were just listed here (sign-in names).
SELECT r.name AS role, u.email, u.is_active FROM users u JOIN roles r ON r.id = u.role_id ORDER BY u.role_id, u.id;
