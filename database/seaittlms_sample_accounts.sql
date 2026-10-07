-- Sample sign-ins for SEAITT e-LMS: 1 administrator, 1 registrar, 1 guidance counselor, 12 teachers and 40 students.
--
-- How to run it: open this file in MySQL Workbench (File > Open SQL Script...), then press the lightning bolt (Execute).
-- The tables must exist already (start the app once, or run seaittlms_reset_and_rebuild.sql first).
-- It is safe to run again: a person is only added when they are not there yet. The school has exactly ONE administrator, ONE
-- registrar and ONE guidance counselor, so for those roles nothing is added when the role already has an account
-- (the app makes the first administrator by itself from the Seed:Admin settings in appsettings.Local.json).
--
-- The teachers and the students are plain accounts. They have no courses or enrollments yet: the administrator gives a teacher
-- courses (Users Management > Teachers > Courses), and the registrar enrolls the students (Enrollments, type the LRN: the account
-- is found). Until then the students are listed under School setup > Not enrolled.
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
    (1, 'admin@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$AHAE/4C2Bh7rGzhhjrSbvQ==$m3Tt1DElHIDR26MZ0sx25kDREjcx/k2BNWCWcgtxDBI=', '5cf1aa109cc9484a8524fc031aeda12f', 'Maria', 'Cristina', 'Santos', 'A-001', NULL),
    (4, 'registrar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$8Mbxwf8VwCvXlJm503ihsg==$3qngpQwbpDUWUapMfQOTAUBwYi+HZt9EwwSDKZE6918=', '0dc1569621f342dcac8c0278f834ca4f', 'Rosa', NULL, 'Mendoza', 'R-001', NULL),
    (5, 'guidance@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$nhpIOXGi9tU8+YlIlffgTg==$Tm4BjUEIFoIqqokaSMiGx+5CXOy9g8h+YK/w00aXR4s=', '397384ecc0024f58a5ce8aff255577f8', 'Gloria', NULL, 'Reyes', 'G-001', NULL),
    (2, 'ricardo.villanueva@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$fNPXH4e8HDYOUdADUQlVCQ==$IkfR0k3Vn/fTuYJL2kI5HRYzqnBeSf7fAkvBNnNzLo4=', '2d28807d8b8f4f519c9d9375a8973656', 'Ricardo', 'Dizon', 'Villanueva', 'T-001', NULL),
    (2, 'angelica.bautista@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$3oNyK4vtHRyhFVkdfHQ3HQ==$D5mxcyxVSJF0Mohhz2AGDv93+HpxqdRifgzAVO7XpvY=', 'ddda3227219a463bb4a3e287b4191534', 'Angelica', 'Lopez', 'Bautista', 'T-002', NULL),
    (2, 'eduardo.navarro@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$tgmS5CMgSAtCXoztsLxLOA==$QvB4X2Upt0L3ybkeEcbHNFX8tWnWHQW2U7S8CLAzQHA=', '458f435767bc42a69907579031ca866e', 'Eduardo', NULL, 'Navarro', 'T-003', NULL),
    (2, 'marites.domingo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$mUg4tZqsa/ewDr7HyR71qQ==$tj0JMcOYhPj9ZXgMq0b9L+qy62vIVt1oGE1D1pcG8Js=', 'e33058fb8b094f16a2f1e4299c34e172', 'Marites', 'Cruz', 'Domingo', 'T-004', NULL),
    (2, 'jonathan.aquino@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$DPYogq3bEwBj/NiSCnJMog==$YjrlQtsiklN/9CKj7CSz0OYr2xdUsDB3Vdyajm3K2jc=', '3536716e852e4dfd853aaaa34a129fd0', 'Jonathan', NULL, 'Aquino', 'T-005', NULL),
    (2, 'cristina.fernandez@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$vevqt467i0maw6EeMlaWDg==$6pKdnDNtbjqnlA3OeGgQaqo/VKcCSOv104KsTJXCCj8=', '07132b677fd54dbab9750708cf6616d0', 'Cristina', 'Reyes', 'Fernandez', 'T-006', NULL),
    (2, 'roberto.castillo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ApKYvLCL6SKuu2RJxwsJCw==$B23Ym7hQEBEAIn08l6PAblyJ+Pc7HkdFSqUtX8zg4OM=', '6ca9a25ee52546678a7f0eb03377ae97', 'Roberto', 'Garcia', 'Castillo', 'T-007', NULL),
    (2, 'liza.ramos@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$YkflVk0Pe66GBpppRKiXXA==$3FEdpQhNOb/OXqkx381xx125dk16R/YVFDPXe4jzdxo=', '7e80dea787f1462994e30ca8934601c0', 'Liza', NULL, 'Ramos', 'T-008', NULL),
    (2, 'mark.salazar@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$fk1X4CMWLnmtYYym7kD8jA==$4WvoJe2W2UthyhGJ+8dyzVnmrKEuRIA8tkUmLFiLMuU=', '93998d6ad54d4e7ab4576e0b192c5de1', 'Mark Anthony', 'Perez', 'Salazar', 'T-009', NULL),
    (2, 'teresita.pascual@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$vDW5EkVHjBc/23AaWJmCbA==$oilug+B38idNDhIN4Cjd0jBZVSvtsuddfVCXlcQDjY8=', 'caf46f8aa29f4299a7cd8cc17b37e7bf', 'Teresita', NULL, 'Pascual', 'T-010', NULL),
    (2, 'gabriel.ocampo@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$w7GvIBwqoQ8lpUyUPuoZ1w==$5sq7UzjUFrlqHH5YLn2+o90Qrgb5aazQLwAwY7heHJk=', '7397bc8fb3ef4952b0852cae8f135b18', 'Gabriel', 'Tan', 'Ocampo', 'T-011', NULL),
    (2, 'shiela.magtanggol@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$y/ztBRoy+F+aOZQQB3X7Kw==$fNo01ku/zWfmYBh4oCzN5Orsbqv9aWucTbZZiHaWqqQ=', '930693afae8e451499bd3aa9c130fed2', 'Shiela Marie', 'Uy', 'Magtanggol', 'T-012', NULL),
    (3, 'delacruz.789012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$v9boO1y1J5wlAodKHNY0yw==$1EQ46lvchXdcjWxqb9EK3pv94nWUSAibT9deIhl9DN8=', '5450c43022d24661a78a7390eb1ffb98', 'Juan', 'Santos', 'Dela Cruz', NULL, '123456789012'),
    (3, 'abad.600001@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$lRErBDVu7HVBVUDrBzKiQw==$Y0S4Re12/K4QONP5RnxDzUNopaEPx0nd+Dy7S+lB0Zw=', '13ea3df5d8f74acc942d1c341d4f8e1b', 'Mariel', 'Aguilar', 'Abad', NULL, '105202600001'),
    (3, 'alcantara.600002@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$6/hWWAud+4vH8d2JJGZf3w==$paWfeUyDs7jgS5hejDmqipaU+lklO9Ou7qUjcNJYw5w=', '6689bd9508b148b6a70d73f8ae1fd15f', 'Carlo', 'Bautista', 'Alcantara', NULL, '105202600002'),
    (3, 'andrada.600003@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$S6c/zagp4l0Vg2MSMq9Ggg==$lJ2ROtmCRwBrn5/P4Ou1nFFCXv/jbBoZJfsvz358SBI=', '299fc82f2a634757b87d4e221dc7974e', 'Jasmine', 'Cruz', 'Andrada', NULL, '105202600003'),
    (3, 'arellano.600004@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$pNd7F3yaNzXoSnc9jP+JfQ==$Nk0meCcC8jA9lImTRnogu+7zculso3uPERysUXSBLi0=', '3a1b082d9ae64cb8b49321a10a24a028', 'Miguel', 'Diaz', 'Arellano', NULL, '105202600004'),
    (3, 'balagtas.600005@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$e3mVUNmvRBi4GRw+rJGGag==$7NoPO6Hf4NxvSzI0tupZhZRUCEklicX4UlziCoVc9HI=', '0c0b9f21010f4d0bbffeda0b6ac0980e', 'Hannah', 'Estrada', 'Balagtas', NULL, '105202600005'),
    (3, 'barrientos.600006@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$dWHYUpM4fyqjIL1M3UcyDQ==$44HGoVj3wFi/9ueVeg6dusEzABoyN2N6OT+5rI2zh2U=', '94ea2c78e139408db1c12a9a1a21fd78', 'Paolo', 'Flores', 'Barrientos', NULL, '105202600006'),
    (3, 'buenaventura.600007@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$RHRtt+EdBUrfvLmMAn9t2g==$aMBCJPq2lOfPDnn9nOoKLVWuvi4Ex4kpblZPHNXtXkQ=', '8315e6f2d8b840d1b418a2d0fbfbb2c8', 'Kristine', 'Gomez', 'Buenaventura', NULL, '105202600007'),
    (3, 'cabrera.600008@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$o+Us25jRKigs5AxoRWzWgw==$vvFI2qyW2WDNnDysyJe9AkDGNeWYWFMNUTq2qJA1nDE=', '908fba512185460c9e321f6523f9eebb', 'Joshua', 'Hernandez', 'Cabrera', NULL, '105202600008'),
    (3, 'calderon.600009@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$vKmDkW2LwlGzbxpt6sVBIw==$Ptg1E0QU269acohWEn+HZuRJsMyV+9TYHfC9nUTl0/Y=', '8ed78335b0b6471fbfb67093d8fc6bd1', 'Angel', 'Ibarra', 'Calderon', NULL, '105202600009'),
    (3, 'dacillo.600010@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$TOOGkZT5ulF1y6i87dqaWQ==$VaADAaNP4Lh3w+T2iRCdneE3sWj+CZg0ekfOov7haUE=', 'b64d1b0063474db4a04a39350b9b2e9d', 'Nathan', 'Jimenez', 'Dacillo', NULL, '105202600010'),
    (3, 'dumalag.600011@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$y1FhU1uVDRnDnzXkQyBAlw==$N7gSfF3r5l0doJss/oqFWM0lXVZYUU19rMtjiNlsX3k=', '9f61c6acd33a4ef6983e1e4857b722cb', 'Bea', 'Katigbak', 'Dumalag', NULL, '105202600011'),
    (3, 'escalante.600012@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$eocuH2ejWZLPpHunZ6pBtA==$l/jmsHLOGuA3Cm27I8DnRoeXgn3PsagbO10MGch9Ffs=', '6e337a67933543f89980e4109836a281', 'Ryan', 'Lacson', 'Escalante', NULL, '105202600012'),
    (3, 'espinosa.600013@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$jcpAEgJIXiNa1aFG5MPFLQ==$6HOaaQkEwAVAKlhkZ4Z+PFLPg6IGyODKdZeHwvCnZHU=', 'ea81b8b3436c4ba3a422315028560e89', 'Camille', 'Manalo', 'Espinosa', NULL, '105202600013'),
    (3, 'fajardo.600014@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$N84VYU8M+OBpdrowpOgchw==$GlzFgNwq/I2n4RXY6x3XqrT0Mmf6kTZcuzuahNhkjCQ=', 'c616c2c8decc4fc283f50c2d5b8c69fd', 'Daniel', 'Natividad', 'Fajardo', NULL, '105202600014'),
    (3, 'gonzales.600015@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$WeqcyF0zPmXsKymYoB5E/g==$PXSsgtKTWte+mad22d2nZhVkkoJlbcAHJ23z3oMknRs=', '564628195d2547309ff1452a49abef41', 'Trisha', 'Ong', 'Gonzales', NULL, '105202600015'),
    (3, 'guevarra.600016@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Xi2z5oG/kl7NW8kn0dMHEQ==$mqZCnLoowwBG6/Q1XRUtKpYFTxpOe8V8VvGOsardp7Y=', '9b95877fc3f04bdc9f96a5c20f3a2c2d', 'Kevin', 'Pineda', 'Guevarra', NULL, '105202600016'),
    (3, 'ibanez.600017@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$plNnlDzEWbZTR0yyU/XMpA==$g1646otsl0SQFnwQ6dpcy7VIOxGiZ9OYB1MTxU99RkM=', 'd300d310f72243a8a567aee9073881d8', 'Alyssa', 'Quizon', 'Ibanez', NULL, '105202600017'),
    (3, 'jacinto.600018@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$qm9XTTy1JTAlGAYkYP/Azg==$ec4760UZiDe38T4JDPWh0rHpaGfEwZmomeAo6oP2DH0=', '2b43d9e72c63483aa1e14ca0db1e0720', 'Jerome', 'Robles', 'Jacinto', NULL, '105202600018'),
    (3, 'lagman.600019@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Vmc6Sl40tih4ig83A+lpNA==$IJSiibmUp1icyJk5QBXTU/ztJm2NPpcPm5wUn8FpBPo=', 'b6669ec5ac2149eab88f6596266fb1fd', 'Patricia', 'Sarmiento', 'Lagman', NULL, '105202600019'),
    (3, 'macaraeg.600020@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$WaCWpvl1ARmF/+yCNRMS5A==$080q61CLViPRHmArF06ZulS3+YsXIdFZ51NQemmxZuw=', '8432f7b95303498cb4da0840819335e1', 'Christian', 'Tolentino', 'Macaraeg', NULL, '105202600020'),
    (3, 'madrigal.600021@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Jwyc2LzcvM8r9MjXsJUu7g==$wOAiX57oDnlAGePB8Hq92J7bZRyRuwKr8bkkjjPN0Eg=', '59d99df5b38b4262a2e1a4caef9616a3', 'Janine', 'Umali', 'Madrigal', NULL, '105202600021'),
    (3, 'magsaysay.600022@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$6sdT6yURQXidwtW7648g4w==$gzw2btTl1dZf2R0/vuIqu9sZMb6nbj5sNAVkS/tYXXk=', '64774e97ad12493383bdbc4fc8e1b478', 'Vincent', 'Valdez', 'Magsaysay', NULL, '105202600022'),
    (3, 'mercado.600023@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$RRuJomyH8c7cWi+2eRzAcA==$iGpIDINGjt8q8e1T6NaWWIokB3uSLKwugrj1ab7IFhE=', 'c362abd47daa4586a3950be88e4da396', 'Nicole', 'Yap', 'Mercado', NULL, '105202600023'),
    (3, 'mojica.600024@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$77PJjkhEqo/2XZMW0bYCmg==$essCe+FPXEtL1g2+4yNfTDJSZQvXGfIDlqZwr5VI0Ns=', 'ccad69b34c9945cf96915ba705dbf307', 'Adrian', 'Zamora', 'Mojica', NULL, '105202600024'),
    (3, 'olivares.600025@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Qtmu7G1qq9Orz2VAcETkrw==$z5NTemRSzd8xSCWDgzZ93DjMVgByv3mQqn8fYJOG2Xo=', 'a63ed644d3c647c3a01b997f1ef92f25', 'Samantha', 'Abella', 'Olivares', NULL, '105202600025'),
    (3, 'palma.600026@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$lnNaB2V0WC5hlYBYtb3HEA==$pZ2BqFynJ9pU6XXHt+2qoZeb6r73FJfva3Opzv69djc=', 'ded07a98039c42d8bc0a61ee9ae79ca8', 'Francis', 'Bernardo', 'Palma', NULL, '105202600026'),
    (3, 'quirante.600027@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$2lnbCXdMJelGkZcQT8tTXQ==$zM2gif8GWZNqAew5XLBiaL/yUeL/cGwr1speXLxA+JQ=', '45a7d4f8d27d4bc4a8a0e69f41054ebb', 'Louise', 'Cabral', 'Quirante', NULL, '105202600027'),
    (3, 'rafael.600028@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$2aLd6LQMJ89/cbQ89u6WNQ==$IRhyu8dhO7buNeI7zCBD0P020kMoc9zRLaLJDZMj1po=', 'a63c8d3b9c844324a106657ccbe08d4d', 'Marco', 'Delos Reyes', 'Rafael', NULL, '105202600028'),
    (3, 'sandoval.600029@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$tiL7KPK7LYkwBCsL8Vq4qw==$kifIoEiEqKcVKm2Bk0KKwGA4XRIVU1R+J0lvTB7PrVM=', 'b3db15f72d6a4fcdbdca390ea8ae501f', 'Isabel', 'Enriquez', 'Sandoval', NULL, '105202600029'),
    (3, 'tanedo.600030@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$QcYfwWT9vEOuIojMsL3kAw==$rxtjjCA11l6kKAHH/QcCPedgfumOK4ZA/uCGMiJq86U=', '7335857e4e1f417db26ca0b7a3bacc91', 'Gerald', 'Fuentes', 'Tañedo', NULL, '105202600030'),
    (3, 'torres.600031@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$1S15CrN1U1/2hKNFwR3vfg==$u9jwh3xhBkcVmTDO6h+wt4+uj09yV41WjRXnpNwDzYo=', '3759a280639f497f881c71e6b2eaa6ea', 'Danica', 'Gutierrez', 'Torres', NULL, '105202600031'),
    (3, 'urbano.600032@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$ZvQXQkR+Ka4foI6ZyiwfAQ==$ew2oLEIYfYUJ+CD92CiKceioeLMy/NRjbkCmCI1KilA=', '4337b37ba202441a87185a6eb0c7453f', 'Emmanuel', 'Hizon', 'Urbano', NULL, '105202600032'),
    (3, 'vergara.600033@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$VtmY+vbDf57JFloHLlDpqQ==$gtTkdEqQWgf223BQD3PhuN6iqGT2bf27EqIhfSLXRZg=', '75618fc788774c5b9f4f145c68ae74e6', 'Rhea', 'Ignacio', 'Vergara', NULL, '105202600033'),
    (3, 'villamor.600034@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$k9RCIbJ2Jx3T+86JymYG9g==$UtcsmzWW/yEUAQMiqTm2lr3OQGGJE5Rv3IVpQoWfkOQ=', '11d13cb76b054a37a5e5b0770dfe72a2', 'Lance', 'Jose', 'Villamor', NULL, '105202600034'),
    (3, 'yambao.600035@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$jWRzpxdXAeZJkgPdZYVltg==$vDEP7KBayT3rer0N5wM3ABQgAljhhEv/FzwASL9ALLE=', '386b5203326047a396ad5eb3643e804d', 'Maricar', 'Lim', 'Yambao', NULL, '105202600035'),
    (3, 'zafra.600036@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$z6WfCHRXBBs8U84ierk8qg==$zLz2Zwlgsly1D771y0SD9khV7X8oTPOMkhv2kJWuo+g=', '7c72441658c7429bb6cf336190f30775', 'Oscar', 'Moreno', 'Zafra', NULL, '105202600036'),
    (3, 'ancheta.600037@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$pEfrZJzNwDNrJb2TEw6dPg==$mfMvEVFuBYoR/sf//U8xNi7JCcXN9eBaOknjnPkcyQI=', 'eb7e0cf744ba4907b758d79c1a702eda', 'Princess', 'Nolasco', 'Ancheta', NULL, '105202600037'),
    (3, 'bonifacio.600038@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$xVshCR03zSO+rx/RU+pyuQ==$KGroMZJz3NV2V6YLc3lvPJQYTk8FkvncD6eDd5f4TbM=', '55ff086e955f46e0b5b707065931e25a', 'Harvey', 'Ocampo', 'Bonifacio', NULL, '105202600038'),
    (3, 'corpuz.600039@legazpi.seaitt.edu.ph', 'PBKDF2-SHA256$600000$Vi4l/LDIpsE2mokxBjrYNQ==$vujSZEdinojKtHIOy9S/2mc+qgbGt4ASPndoAiQtMR4=', '675eb61f165d496dafaaa007eda8e44b', 'Faith', 'Pacheco', 'Corpuz', NULL, '105202600039');

-- 1) The accounts. Role 1 = administrator, 2 = teacher, 3 = student, 4 = registrar, 5 = guidance counselor.
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
