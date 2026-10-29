-- Sample sign-ins for SEAITT Legazpi LMS: 1 administrator, 1 registrar, 1 guidance counselor, 1 academic coordinator, 1 academic provider (central team), 12 teachers and 40 students.
--
-- The sign-in of each person is made from the name and the number they have at school, the same way the app makes the sign-in of a new person:
--   * staff and teachers: the last name, a dot and the digits of the employee number     for example villanueva.001@legazpi.seaitt.edu.ph (employee number T-001);
--   * students: the last name, a dot and the last 6 digits of the LRN                    for example jacinto.111111@legazpi.seaitt.edu.ph (LRN 105202111111);
--   * the administrator keeps admin@legazpi.seaitt.edu.ph.
-- The passwords are not written in this file (only their hashes are). The list of the sign-ins is kept out of the repository.
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
-- chose there). THE PASSWORD OF AN ADMINISTRATOR IS NEVER CHANGED by this file. To give that administrator the sample password of the list of sign-ins too, run the
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
    (1, 'admin@legazpi.seaitt.edu.ph', 'admin@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$1e/xE3DL4aF3ms5tGruB4Q==$hDr7Ju6FRkCr0Om7smrPd6caEHkoZD9ZU5fu0P1CZDY=', '89923d42c0104c1283ff5cae83828b7b', 'Maria', 'Cristina', 'Santos', 'A-001', NULL, NULL),
    (4, 'mendoza.021@legazpi.seaitt.edu.ph', 'registrar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$3ire9vAoVfNA82+3gQkPEQ==$QHKMyvZPcn1K7YSOcwbKzD8U9oAt4xdtW1ud1yjIp70=', 'f4469f1182c644338c37089a70a2a563', 'Rosa', NULL, 'Mendoza', 'R-021', NULL, NULL),
    (5, 'reyes.022@legazpi.seaitt.edu.ph', 'guidance@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$zCqxlTkXDtJU13uem9E6ng==$gvMBiaossNl4bGWj+juIKseoUEvl7I5nkpVOJyItUbI=', 'e74423293b6e4e58b232040075e70e9a', 'Gloria', NULL, 'Reyes', 'G-022', NULL, NULL),
    (6, 'villareal.023@legazpi.seaitt.edu.ph', 'coordinator1@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$7OnvImMDOU5mi2SPN9IGQw==$FHCqwwL8djISRbrmX3lhxzdNA83kxhWIfgMZqVvIOG0=', 'ef1724406a544ec1a1c4c106f824b0c3', 'Lourdes', 'Garcia', 'Villareal', 'C-023', NULL, NULL),
    (7, 'pangilinan.024@legazpi.seaitt.edu.ph', 'provider1@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$4bRCt5aBUeqzgMqo6ZcB6w==$dMcm49R19nIrKUWSkGOtZ3xmwPJkswT2y6fp1ixSDas=', '70ac06d8b6ef4f38a003a36417059864', 'Elena', 'Cruz', 'Pangilinan', 'P-024', NULL, NULL),
    (2, 'vargas.013@legazpi.seaitt.edu.ph', 'vargas@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$sHC8daSgqvfjanx2Uc/5tg==$AUI1uMNUiFV3i9k1EhRf/KVjJqxlxmLSjwS97s7ZJKM=', '11c4c5d178f241b6a57c88e0c23ebe43', 'Rafael', 'Mendoza', 'Vargas', 'T-013', NULL, NULL),
    (2, 'villanueva.001@legazpi.seaitt.edu.ph', 'villanueva@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$p2KAcyDfrKqcZXw4bb+w/g==$iGRbgaujDbEBDZTVTxiydzy49zoSAISbBY8Ww7iRz48=', '5e90eff5b5c241bb90735eb13d15c97d', 'Ricardo', 'Dizon', 'Villanueva', 'T-001', NULL, NULL),
    (2, 'bautista.002@legazpi.seaitt.edu.ph', 'bautista@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$LHHVUsLi3KDtL1sq8i46HA==$glMMCN+MFYPZqdc5x6x+Nz6Dnv94wfqAs9BbF+JwFMg=', 'd72266c7c4d14ca7aca04841b487728c', 'Angelica', 'Lopez', 'Bautista', 'T-002', NULL, NULL),
    (2, 'navarro.003@legazpi.seaitt.edu.ph', 'navarro@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$shPQSQX3fiTPiw40d+OeaA==$uQtUmFUrvPOINGhQfFRMAA7PnHDg0cBH4aIQIapE73Q=', '20808db89cb14053b4c4d677340639a6', 'Eduardo', NULL, 'Navarro', 'T-003', NULL, NULL),
    (2, 'domingo.004@legazpi.seaitt.edu.ph', 'domingo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$homjfSutIfBvJi3bWNLRRA==$YTEdvAHVDKk+rZj3iiyHyGkujlcKho/9+TcMFJEmieQ=', 'fe1eef1d0b354096b4ee214638f77b0f', 'Marites', 'Cruz', 'Domingo', 'T-004', NULL, NULL),
    (2, 'aquino.005@legazpi.seaitt.edu.ph', 'aquino@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$x9rGNDmkxIBw734gUjG3Bw==$IP5SQsdorc2pr3ck8DK1ulMv+7OWR/3Jf7V1+AptP9w=', '7f369214556a425baaef4a3e24849fdc', 'Jonathan', NULL, 'Aquino', 'T-005', NULL, NULL),
    (2, 'castillo.006@legazpi.seaitt.edu.ph', 'castillo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Tb1+kqYJwX/H5kygVspO5g==$0gAqTY/sS/kpylNLjRkuePujzZE2+c7c1CLt2JlYITc=', '8795e15468ed42a0b9dd8aa2dc16edda', 'Liza', 'Ramos', 'Castillo', 'T-006', NULL, NULL),
    (2, 'mercado.007@legazpi.seaitt.edu.ph', 'mercado@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$btTGuuBZ+sKvhKyPg3FnRA==$XX3dnjuXtCcYn146qEMOhmzd/LesSpaYo1Q2eZquSfg=', 'f1ab70c0ee60428993ceea9b6f9f17c4', 'Benjamin', NULL, 'Mercado', 'T-007', NULL, NULL),
    (2, 'ocampo.008@legazpi.seaitt.edu.ph', 'ocampo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$qYNNeI3GgLc1U7xMJy8SFg==$pI/i/Jy4kQof87C30DYH+soaLuLSBWhOzTg6HvDx0FA=', 'b46f03f8ddb04f9f8cfbd193bcc747d0', 'Teresita', 'Lim', 'Ocampo', 'T-008', NULL, NULL),
    (2, 'pascual.009@legazpi.seaitt.edu.ph', 'pascual@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$NZTYz4+4os65svVkxrCseg==$L2f3bEjlCylXo1GGC+DVc+E2Q24/8fAN+uZUwAnAFhw=', '0dc718436cbc46c3805cedfe06f36d26', 'Ernesto', 'Reyes', 'Pascual', 'T-009', NULL, NULL),
    (2, 'soriano.010@legazpi.seaitt.edu.ph', 'soriano@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$wjEDwRShr9I3yBQyz0HiZA==$Qp4SSmk66YSfUtxsbU7LWhU5iVlqRtVj3BltEdeiZJo=', '1e4cbf5bb0eb4a4fb6413dc946bb45b6', 'Carmela', NULL, 'Soriano', 'T-010', NULL, NULL),
    (2, 'tolentino.011@legazpi.seaitt.edu.ph', 'tolentino@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$0+QoPRfHe6dfkXX1wOSzHA==$syhwBYzXPR6lUQqmmTb/GgK4gmoJF4cpnybYdPvTpC8=', '490c69779ce34f869fb09f356ec5ed86', 'Rodrigo', 'Tan', 'Tolentino', 'T-011', NULL, NULL),
    (3, 'delacruz.789012@legazpi.seaitt.edu.ph', 'delacruz@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$2EVubAS5NxklgO+07T8JTQ==$wBtM8iCew1SLKxxRKxG80MMEybZov6XkmtK+1P0HVVM=', '4030267ea9424a868d40fde509fbcb05', 'Juan', 'Santos', 'Dela Cruz', NULL, '123456789012', NULL),
    (3, 'abad.600001@legazpi.seaitt.edu.ph', 'abad@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$cX1WZVZL1xY+24JD3WG2YA==$nDHmyW+5MnZ+rEEuvMoXvUi7S5WsZuxfvG7C7zpiVUs=', '416f8e9951a0430e8d3e6f708231fff4', 'Mariel', 'Aguilar', 'Abad', NULL, '105202600001', NULL),
    (3, 'alcantara.600002@legazpi.seaitt.edu.ph', 'alcantara@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$9NN8c6VbQnjYVx1MNzpLfg==$RlBJiNtRRZRDcflarIzKdmS2idzxCVZVNDPInHMrLts=', '28869335bcbe49bfbd7b2d7e62fafa98', 'Carlo', 'Bautista', 'Alcantara', NULL, '105202600002', NULL),
    (3, 'balagtas.600005@legazpi.seaitt.edu.ph', 'balagtas@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$I30ZOR/tVzT/pNd2gCBvog==$MA4j42whtWXj2HjUM373XDeFC/IsFOGxqUY0cSFg6wQ=', 'da3aa394438b4b2a92d28d31f1c426ac', 'Hannah', 'Estrada', 'Balagtas', NULL, '105202600005', NULL),
    (3, 'barrientos.600006@legazpi.seaitt.edu.ph', 'barrientos@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$1+cIinwjaU6O4bMnSSoPaQ==$nFsYj9EgelVCOsOve2rTrW6DOshjjqcKw95x3KbJNr4=', 'a89760f10be64a9ea08fcbfa5835f958', 'Paolo', 'Flores', 'Barrientos', NULL, '105202600006', 'Jr.'),
    (3, 'buenaventura.600007@legazpi.seaitt.edu.ph', 'buenaventura@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$NECWyVydrRHqQ9R/WX73eA==$zjadTy3xRMEKgtgN4yFnV0EwTMAV9X68QHasvNns8zQ=', '1952ed5f65414cdf83e718e1aa7448e3', 'Kristine', 'Gomez', 'Buenaventura', NULL, '105202600007', NULL),
    (3, 'dacillo.600010@legazpi.seaitt.edu.ph', 'dacillo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ZP2rfw8qX0yE9tHCJjD3CA==$a7UxWc2x/2gWSL0BBu/DQed6Jzr2DdahtZFvHfSb66A=', '9e8c4bae18054a8bab5bad00dd4c2945', 'Nathan', 'Jimenez', 'Dacillo', NULL, '105202600010', NULL),
    (3, 'dumalag.600011@legazpi.seaitt.edu.ph', 'dumalag@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$+lCPerEJxUZOHUd9JM6qwg==$U4VSY7l6OqgI9KBRaRFzaBDafKGduLQsAZvMQu2u/68=', 'f9b25c2f254a440184c4ca35d3d19ed6', 'Bea', 'Katigbak', 'Dumalag', NULL, '105202600011', NULL),
    (3, 'escalante.600012@legazpi.seaitt.edu.ph', 'escalante@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$8wSzwKkP2LpjDSPnp2VReQ==$VimvAx7Dhm3pkjSkV4qzZE7eVp6i1FJW8Dq9kOEI+Yo=', 'd99e220e39bb4df4a18d2678b82cabc5', 'Ryan', 'Lacson', 'Escalante', NULL, '105202600012', NULL),
    (3, 'gonzales.600015@legazpi.seaitt.edu.ph', 'gonzales@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$zQZsCRNmoOfJtuXT0bQSNQ==$hVtdaAshTvNQvcOmUzeDMi+HB49j6zB1LxSEfTtpuyE=', '5a29e221fd27426bb3586d89855fc302', 'Trisha', 'Ong', 'Gonzales', NULL, '105202600015', NULL),
    (3, 'guevarra.600016@legazpi.seaitt.edu.ph', 'guevarra@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$2bjmBOXe9mKJRntSCu+DLg==$WPOcYUONMOrOCKDbL2lETd4L84/A6FKSGydfblp5QUs=', '0b96ad65e5ff48809e250767416fa2e6', 'Kevin', 'Pineda', 'Guevarra', NULL, '105202600016', NULL),
    (3, 'ibanez.600017@legazpi.seaitt.edu.ph', 'ibanez@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$T3S+S8dQ8X4oBgExw0YgwQ==$R5kwW+Gsmz75ZGGqSOHitY8Jp1QFCN8TFPf/nwI09ok=', 'e6843e7533a74b2c8623dfdb997b967f', 'Alyssa', 'Quizon', 'Ibanez', NULL, '105202600017', NULL),
    (3, 'jacinto.111111@legazpi.seaitt.edu.ph', 'jacinto@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$3YKRVCJGgW/tpfKzwitguQ==$FKNLJA7ZzsiaGg1G+Cy3XYxiRoJmHmu5e+Xv2AoRfEY=', '5a44fcd78cd24657927abc480d906e84', 'Miguel', 'Reyes', 'Jacinto', NULL, '105202111111', NULL),
    (3, 'lagman.600019@legazpi.seaitt.edu.ph', 'lagman@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$FHb6Qsix6rp+bcNTq1fMmw==$g60lwXHgpvq0c9kMunBI5NoHFx5i5YiIsFJLSIGBtDU=', 'b11852d03de64b8bb41480f6b3870244', 'Sofia', 'Tan', 'Lagman', NULL, '105202600019', NULL),
    (3, 'macaraeg.600020@legazpi.seaitt.edu.ph', 'macaraeg@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$s+F6Gj0vjwDpKqzQPiGmkQ==$HDA8a94DTTR2sCRx5NJhSiCQYUL8sr/UdI7yjf0+hfY=', 'b00638eac5f949b3820f71ce222e6019', 'Gabriel', 'Uy', 'Macaraeg', NULL, '105202600020', NULL),
    (3, 'natividad.600021@legazpi.seaitt.edu.ph', 'natividad@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$jTyeZV/kaAtgthMKLxNQAw==$+xrv2TQ8mhm84/5z5jPn0lzK4GhYrH7WZjSSClNP4lQ=', 'ffdf69c62b304eabbacb87dafd92f639', 'Angela', 'Vera', 'Natividad', NULL, '105202600021', NULL),
    (3, 'obispo.600022@legazpi.seaitt.edu.ph', 'obispo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$pSnm8p6T51XL9AJc7OLy7A==$kSaoK6QJ7a2sErUYyWM4FjY3GJ5jOGAm+oZ+Yzyhm8I=', 'aa2b7abb2f8f4421bb75bfd8d5e69c71', 'Joshua', 'Yap', 'Obispo', NULL, '105202600022', NULL),
    (3, 'quiambao.600023@legazpi.seaitt.edu.ph', 'quiambao@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$G35I0w4Pc2l64Ujno6sNVg==$KLkhV9zCEkb3nca2uwBKc3ug20JzmfssZ68pOYY9HNw=', 'a4965265ef0c40caa724ea23c790543e', 'Camille', 'Zamora', 'Quiambao', NULL, '105202600023', NULL),
    (3, 'rivera.600024@legazpi.seaitt.edu.ph', 'rivera@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Yl328UkvEFuGGTHoXyIHeA==$hezqtqmrYDhbRFUppulX0jLQ9HWqeWhJnKncEBQ17eQ=', '986b2a1805f94def8e8a868abce7fbd5', 'Daniel', 'Abella', 'Rivera', NULL, '105202600024', NULL),
    (3, 'salvador.600025@legazpi.seaitt.edu.ph', 'salvador@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Hr0TFLZkmHxD/02w+tUUHw==$O2qaBUIhl+JB7ifWtpZS92iF68rjnn3rR6FYLoMaP2Y=', '69dcd71bf87e4651bbeaf1435a0c6c2a', 'Jasmine', 'Bonifacio', 'Salvador', NULL, '105202600025', NULL),
    (3, 'umali.600026@legazpi.seaitt.edu.ph', 'umali@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$RnMREE+ysnCwRObA2FJi9A==$kAcZpHRchQwp3OJdzylUubUxGsLecIZiuM/ZImhgtYI=', 'aa730ac3af7742dbb18b1f57940bfa34', 'Lance', 'Cabrera', 'Umali', NULL, '105202600026', NULL),
    (3, 'valdez.600027@legazpi.seaitt.edu.ph', 'valdez@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$cNMnmaOAXTcqGnkzx/9OMQ==$KNd29NTf0oTRaZyQ7kXTp+wrleW47VVBhBX36LUg6xg=', '3368a4b527d7441faff66c804c21be9f', 'Patricia', 'Dizon', 'Valdez', NULL, '105202600027', NULL),
    (3, 'yap.600028@legazpi.seaitt.edu.ph', 'yap@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$s/cAkYgWRO42vXjFS/6M3g==$E2G4x520wAaNkkaBZ1PQEFVDAbuximDAD9hyeH6FGGo=', '4c10fd5bb3ae466a8301f49a52f985af', 'Mark', 'Ebora', 'Yap', NULL, '105202600028', NULL),
    (3, 'zamora.600029@legazpi.seaitt.edu.ph', 'zamora@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ml3ghSY8tIu7cru893UV4w==$r/IOe5379DtP/h2X9uTJCmGb50n2LWD/MgBdy5fcyNk=', '61e040fa56e041ca9d989aeed3ef841c', 'Nicole', 'Francisco', 'Zamora', NULL, '105202600029', NULL),
    (3, 'agbayani.600030@legazpi.seaitt.edu.ph', 'agbayani@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$KTZoXiFnQlGuyfk4Jjrv3w==$Xc5IazcSYegH4q+3amRwoZPAaMTDj7IXzikjVd3IVq0=', '8fc9619244024760bbeaa125da7722f9', 'Adrian', 'Gutierrez', 'Agbayani', NULL, '105202600030', NULL),
    (3, 'bernardo.600031@legazpi.seaitt.edu.ph', 'bernardo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$qM9eB7PX0RJNlx9wfuqxtQ==$ocrS/7t7cFp+xtGXMLQgZay045Db9EHQ72wm6/YlH88=', '07dd0a6b4346475497825326180f0104', 'Bianca', 'Hilario', 'Bernardo', NULL, '105202600031', NULL),
    (3, 'catapang.600032@legazpi.seaitt.edu.ph', 'catapang@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$AaHBGcVTBr9Vl7Gat47GSQ==$OfYwLIUf9u5mBQoCRD7GQZKJld/n7J5KJoDRtT8VqTU=', 'aa6b1b899b16416fb9dae321f924e6c8', 'Christian', 'Inocencio', 'Catapang', NULL, '105202600032', NULL),
    (3, 'dimaculangan.600033@legazpi.seaitt.edu.ph', 'dimaculangan@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$l4MMEF1g9bErAsy5ORnzvA==$M7H3jexSIeCP6mZLZjFHhUjgANknCf6XCypV4MlreO0=', '51d574d69afb40f29cea08bcf07ee45b', 'Diana', 'Jose', 'Dimaculangan', NULL, '105202600033', NULL),
    (3, 'evangelista.600034@legazpi.seaitt.edu.ph', 'evangelista@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$DhJMke3npRMAaQS5Ebs21A==$yUXfAo+Vtha+dPTQNgvxBvHEbK7pzHt0QaSo9VoZgCA=', 'f16e4374dbad47a1b1115ac0c70504b1', 'Enzo', 'Kalaw', 'Evangelista', NULL, '105202600034', NULL),
    (3, 'fajardo.600035@legazpi.seaitt.edu.ph', 'fajardo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$sf9idlCIudlEuL0zgrdaXw==$xnq48KtCrkpZzAerONMgAjAMqpU5rg0tYeVtyaYeekA=', '655c08c10a6443979b7d8da6d120c7a0', 'Faith', 'Lopez', 'Fajardo', NULL, '105202600035', NULL),
    (3, 'galvez.600036@legazpi.seaitt.edu.ph', 'galvez@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$TU4mfZDHQ+E0brrGtocRcw==$18ZmXkzvPtap/JyU/4lVmtTvTy6BI+JwWVOpHWGA72g=', 'fbc2c72143eb45d4995b5963b784be3c', 'Gian', 'Manalo', 'Galvez', NULL, '105202600036', NULL),
    (3, 'hizon.600037@legazpi.seaitt.edu.ph', 'hizon@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$w69IwLcw/weGvP2oTFOb9w==$pTH9P2IgmZ5usqmM/kwCf7I14eFeVgJZOmjeDa9TxGg=', 'e5dcaa294e504e2c838475da68a2a0ff', 'Hazel', 'Nuñez', 'Hizon', NULL, '105202600037', NULL),
    (3, 'ilagan.600038@legazpi.seaitt.edu.ph', 'ilagan@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$6Wt9gjPl3Fps7K4XbVwlhw==$sHh/a4HQAvDBi13D6yyGV+L3//XpjXaGAbvQsa+CJ/M=', 'fcd2555061434af1b4d81ff7661a49d1', 'Isaac', 'Ortega', 'Ilagan', NULL, '105202600038', NULL),
    (3, 'javier.600039@legazpi.seaitt.edu.ph', 'javier@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$2vZdyPc0s3QCQG6jMw4iaw==$xKhY97j2RXbAZWQvDoKbSDx4euF9kOWU3QCSwqhiyVY=', '8dc728e5606b41c2bfbcf92f4ba60140', 'Janine', 'Perez', 'Javier', NULL, '105202600039', NULL),
    (3, 'lazaro.600040@legazpi.seaitt.edu.ph', 'lazaro@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$DsmqVEUa+oj0qcC3WU1OJg==$NSmylsZXmE4aiE9OT6fNJ8LyEc4wqwvPWGU2rDr/95g=', '476701d536f84dfd8d8e08997d112b64', 'Kyle', 'Quinto', 'Lazaro', NULL, '105202600040', NULL),
    (3, 'maglaya.600041@legazpi.seaitt.edu.ph', 'maglaya@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$R8rJXwYogH+sMMV0/LIRPw==$loCU+O1BNcUKVaatg3uzzTuqevPdf/nnhdcNyQ3cuqs=', '59117f32d2db4e55acca415b03b9d391', 'Lara', 'Robles', 'Maglaya', NULL, '105202600041', NULL),
    (3, 'nepomuceno.600042@legazpi.seaitt.edu.ph', 'nepomuceno@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$lW0Cjs/L1iiQfX1BfSI2zg==$PzXOgWr+4vQ2SdFsV0s1ZOXTeushO3CwuTBmNibUqzQ=', 'f64446b652c141ba80a78edb45ad5cbe', 'Marco', 'Samson', 'Nepomuceno', NULL, '105202600042', NULL),
    (3, 'pacheco.600043@legazpi.seaitt.edu.ph', 'pacheco@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$S0c8R6lmX0iSxPc4FGyArQ==$l0LfiRI2iYq6guAA3IF9tEKdiReEpk/XWwkXCBxNoGo=', 'ebd12e1acdf34620ad8983ecaee6676b', 'Nadine', 'Tuazon', 'Pacheco', NULL, '105202600043', NULL),
    (3, 'ramirez.600044@legazpi.seaitt.edu.ph', 'ramirez@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$mWh2VQyaLQUcRFcYcIRy4A==$KGXB1DwVKBSpl4NrPn6VhPynN3rQJCjY+G7Ng8MsIPk=', '9019bbc8edf749ffb4682d7d302e03c0', 'Oscar', 'Ubaldo', 'Ramirez', NULL, '105202600044', NULL),
    (3, 'sarmiento.600045@legazpi.seaitt.edu.ph', 'sarmiento@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$lchsWY+zcVqlS8BEZXdc/Q==$H82k9LkmL42nn8B8ms/S/657o6qD0168a3qir4zAHPc=', 'c6a0d864671f438faed4b3d769a3dad6', 'Princess', 'Villanueva', 'Sarmiento', NULL, '105202600045', NULL);

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

-- OPTIONAL: to give the administrator the sample password of the list of sign-ins as well (only for trying the system), remove the two dashes in front of the lines below
-- and run them. Use the email of your administrator.
-- UPDATE users SET password_hash = 'PBKDF2-SHA256$600000$z/1oVZfzFT3tD1tBXGcSUQ==$2PZZp4hzleFIQ3KwMDOdoACcTGxDchLeFZgh+KNGI9I=', security_stamp = '20baec33c573466ca6e03dd30e31902e', failed_login_count = 0, lockout_end_utc = NULL
--  WHERE role_id = 1 AND email = 'admin@legazpi.seaitt.edu.ph' AND id > 0;
