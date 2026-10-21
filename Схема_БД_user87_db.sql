/* ============================================================================
   Информационная система «Учебные курсы»
   Скрипт развёртывания базы данных

   Среда: Microsoft SQL Server 2019 и выше
   Кодировка: UTF-8 с BOM
   Повторный запуск безопасен: скрипт проверяет наличие базы и таблиц,
   создаёт только отсутствующие объекты и не дублирует данные.

   Состав базы: 10 таблиц, 11 внешних ключей, 6 ограничений CHECK.
   ============================================================================ */

SET NOCOUNT ON;
GO

/* --- создание базы данных -------------------------------------------------- */
IF DB_ID(N'user87_db') IS NULL
BEGIN
    CREATE DATABASE user87_db;
    PRINT 'Создана база данных user87_db';
END
ELSE
    PRINT 'База данных user87_db уже существует';
GO

USE user87_db;
GO

/* --- пользователи и учётные записи ---------------------------------------- */
IF OBJECT_ID(N'dbo.[23_09Polzovateli]', N'U') IS NULL
CREATE TABLE dbo.[23_09Polzovateli]
(
    polzovatel_id  int            IDENTITY(1,1) NOT NULL,
    login          nvarchar(100)  NOT NULL,
    parol          nvarchar(510)  NOT NULL,
    rol            nvarchar(40)   NOT NULL,
    aktivnost      bit            NOT NULL CONSTRAINT DF_23_09Polzovateli_aktivnost DEFAULT (1),
    data_sozdaniya datetime       NOT NULL CONSTRAINT DF_23_09Polzovateli_data DEFAULT (GETDATE()),
    CONSTRAINT PK_23_09Polzovateli PRIMARY KEY (polzovatel_id),
    CONSTRAINT UQ_23_09Polzovateli_login UNIQUE (login),
    CONSTRAINT CK_23_09Polzovateli_rol CHECK (rol IN ('admin', 'teacher', 'student'))
);
PRINT 'Таблица 23_09Polzovateli готова';
GO

/* --- учебные группы -------------------------------------------------------- */
IF OBJECT_ID(N'dbo.[23_09Gruppy]', N'U') IS NULL
CREATE TABLE dbo.[23_09Gruppy]
(
    gruppa_id      int     IDENTITY(1,1) NOT NULL,
    nomer_klassa   int     NOT NULL,
    bukva_klassa   nchar(2) NOT NULL,
    CONSTRAINT PK_23_09Gruppy PRIMARY KEY (gruppa_id),
    CONSTRAINT CK_23_09Gruppy_nomer CHECK (nomer_klassa BETWEEN 1 AND 11)
);
PRINT 'Таблица 23_09Gruppy готова';
GO

/* --- ученики --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.[23_09Ucheniki]', N'U') IS NULL
CREATE TABLE dbo.[23_09Ucheniki]
(
    uchenik_id     int           IDENTITY(1,1) NOT NULL,
    polzovatel_id  int           NOT NULL,
    gruppa_id      int           NOT NULL,
    fio            nvarchar(300) NOT NULL,
    CONSTRAINT PK_23_09Ucheniki PRIMARY KEY (uchenik_id),
    CONSTRAINT UQ_23_09Ucheniki_polzovatel UNIQUE (polzovatel_id),
    CONSTRAINT FK_23_09Ucheniki_polzovatel FOREIGN KEY (polzovatel_id)
        REFERENCES dbo.[23_09Polzovateli] (polzovatel_id),
    CONSTRAINT FK_23_09Ucheniki_gruppa FOREIGN KEY (gruppa_id)
        REFERENCES dbo.[23_09Gruppy] (gruppa_id)
);
PRINT 'Таблица 23_09Ucheniki готова';
GO

/* --- преподаватели ---------------------------------------------------------- */
IF OBJECT_ID(N'dbo.[23_09Prepodavateli]', N'U') IS NULL
CREATE TABLE dbo.[23_09Prepodavateli]
(
    prepodavatel_id int           IDENTITY(1,1) NOT NULL,
    polzovatel_id  int           NOT NULL,
    fio            nvarchar(300) NOT NULL,
    CONSTRAINT PK_23_09Prepodavateli PRIMARY KEY (prepodavatel_id),
    CONSTRAINT UQ_23_09Prepodavateli_polzovatel UNIQUE (polzovatel_id),
    CONSTRAINT FK_23_09Prepodavateli_polzovatel FOREIGN KEY (polzovatel_id)
        REFERENCES dbo.[23_09Polzovateli] (polzovatel_id)
);
PRINT 'Таблица 23_09Prepodavateli готова';
GO

/* --- дисциплины ----------------------------------------------------------- */
IF OBJECT_ID(N'dbo.[23_09Discipliny]', N'U') IS NULL
CREATE TABLE dbo.[23_09Discipliny]
(
    disciplina_id  int            IDENTITY(1,1) NOT NULL,
    nazvanie       nvarchar(200)  NOT NULL,
    opisanie       nvarchar(1000) NULL,
    CONSTRAINT PK_23_09Discipliny PRIMARY KEY (disciplina_id)
);
PRINT 'Таблица 23_09Discipliny готова';
GO

/* --- учебные курсы --------------------------------------------------------- */
IF OBJECT_ID(N'dbo.[23_09Kursy]', N'U') IS NULL
CREATE TABLE dbo.[23_09Kursy]
(
    kurs_id             int            IDENTITY(1,1) NOT NULL,
    disciplina_id      int            NOT NULL,
    prepodavatel_id    int            NOT NULL,
    nazvanie           nvarchar(400)  NOT NULL,
    opisanie           nvarchar(max)  NULL,
    put_k_izobrazheniyu nvarchar(510)  NULL,
    data_sozdaniya     datetime       NOT NULL CONSTRAINT DF_23_09Kursy_data DEFAULT (GETDATE()),
    CONSTRAINT PK_23_09Kursy PRIMARY KEY (kurs_id),
    CONSTRAINT FK_23_09Kursy_disciplina FOREIGN KEY (disciplina_id)
        REFERENCES dbo.[23_09Discipliny] (disciplina_id),
    CONSTRAINT FK_23_09Kursy_prepodavatel FOREIGN KEY (prepodavatel_id)
        REFERENCES dbo.[23_09Prepodavateli] (prepodavatel_id)
);
PRINT 'Таблица 23_09Kursy готова';
GO

/* --- задания --------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.[23_09Zadaniya]', N'U') IS NULL
CREATE TABLE dbo.[23_09Zadaniya]
(
    zadanie_id   int            IDENTITY(1,1) NOT NULL,
    kurs_id      int            NOT NULL,
    nazvanie     nvarchar(400)  NOT NULL,
    opisanie     nvarchar(max)  NOT NULL,
    srok_sdachi  datetime       NOT NULL,
    maks_ball    int            NOT NULL CONSTRAINT DF_23_09Zadaniya_maks DEFAULT (100),
    CONSTRAINT PK_23_09Zadaniya PRIMARY KEY (zadanie_id),
    CONSTRAINT FK_23_09Zadaniya_kurs FOREIGN KEY (kurs_id)
        REFERENCES dbo.[23_09Kursy] (kurs_id),
    CONSTRAINT CK_23_09Zadaniya_maks CHECK (maks_ball > 0)
);
PRINT 'Таблица 23_09Zadaniya готова';
GO

/* --- записи на курсы -------------------------------------------------------- */
IF OBJECT_ID(N'dbo.[23_09ZapisNaKursy]', N'U') IS NULL
CREATE TABLE dbo.[23_09ZapisNaKursy]
(
    zapis_id     int            IDENTITY(1,1) NOT NULL,
    kurs_id      int            NOT NULL,
    uchenik_id   int            NOT NULL,
    data_zapisi  datetime       NOT NULL CONSTRAINT DF_23_09Zapis_data DEFAULT (GETDATE()),
    status       nvarchar(40)   NOT NULL CONSTRAINT DF_23_09Zapis_status DEFAULT ('aktivna'),
    CONSTRAINT PK_23_09ZapisNaKursy PRIMARY KEY (zapis_id),
    CONSTRAINT FK_23_09Zapis_kurs FOREIGN KEY (kurs_id)
        REFERENCES dbo.[23_09Kursy] (kurs_id),
    CONSTRAINT FK_23_09Zapis_uchenik FOREIGN KEY (uchenik_id)
        REFERENCES dbo.[23_09Ucheniki] (uchenik_id),
    CONSTRAINT CK_23_09Zapis_status CHECK (status IN ('aktivna', 'zavershena', 'otmena'))
);
PRINT 'Таблица 23_09ZapisNaKursy готова';
GO

/* --- выполненные работы ---------------------------------------------------- */
IF OBJECT_ID(N'dbo.[23_09Vypolneniya]', N'U') IS NULL
CREATE TABLE dbo.[23_09Vypolneniya]
(
    vypolnenie_id  int            IDENTITY(1,1) NOT NULL,
    zadanie_id    int            NOT NULL,
    uchenik_id    int            NOT NULL,
    otvet         nvarchar(max)  NULL,
    put_k_faylu    nvarchar(510)  NULL,
    data_sdachi   datetime       NULL,
    status        nvarchar(40)   NOT NULL CONSTRAINT DF_23_09Vyp_status DEFAULT ('otpravleno'),
    CONSTRAINT PK_23_09Vypolneniya PRIMARY KEY (vypolnenie_id),
    CONSTRAINT FK_23_09Vyp_zadanie FOREIGN KEY (zadanie_id)
        REFERENCES dbo.[23_09Zadaniya] (zadanie_id),
    CONSTRAINT FK_23_09Vyp_uchenik FOREIGN KEY (uchenik_id)
        REFERENCES dbo.[23_09Ucheniki] (uchenik_id),
    CONSTRAINT CK_23_09Vyp_status CHECK (status IN ('otpravleno', 'provereno', 'naznacheno'))
);
PRINT 'Таблица 23_09Vypolneniya готова';
GO

/* --- оценки ---------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.[23_09Ocenki]', N'U') IS NULL
CREATE TABLE dbo.[23_09Ocenki]
(
    ocenka_id     int            IDENTITY(1,1) NOT NULL,
    vypolnenie_id int            NOT NULL,
    ball          int            NOT NULL,
    kommentariy   nvarchar(max)  NULL,
    data_proverki datetime       NOT NULL CONSTRAINT DF_23_09Ocenki_data DEFAULT (GETDATE()),
    CONSTRAINT PK_23_09Ocenki PRIMARY KEY (ocenka_id),
    CONSTRAINT UQ_23_09Ocenki_vypolnenie UNIQUE (vypolnenie_id),
    CONSTRAINT FK_23_09Ocenki_vypolnenie FOREIGN KEY (vypolnenie_id)
        REFERENCES dbo.[23_09Vypolneniya] (vypolnenie_id),
    CONSTRAINT CK_23_09Ocenki_ball CHECK (ball >= 0)
);
PRINT 'Таблица 23_09Ocenki готова';
GO

/* ============================================================================
   Демонстрационные данные
   Пароль всех пользователей — 12345. В рабочей базе вместо открытых паролей
   хранится PBKDF2-хеш: приложение формирует его при регистрации и при
   первом входе переводит устаревшие пароли в хеш автоматически.
   ============================================================================ */
DECLARE @p_admin int, @p_teacher1 int, @p_teacher2 int,
        @p_student1 int, @p_student2 int, @p_student3 int,
        @g10a int, @g10b int, @g11a int,
        @d_math int, @d_info int, @d_phys int,
        @c_alg int, @c_inf int, @c_geo int, @c_phys int,
        @z1 int, @z2 int, @z3 int,
        @u1 int, @u2 int, @u3 int,
        @pr1 int, @pr2 int;

IF NOT EXISTS (SELECT 1 FROM dbo.[23_09Polzovateli] WHERE login = 'admin1')
BEGIN
    INSERT INTO dbo.[23_09Polzovateli] (login, parol, rol) VALUES ('admin1',    '12345', 'admin');
    INSERT INTO dbo.[23_09Polzovateli] (login, parol, rol) VALUES ('teacher1', '12345', 'teacher');
    INSERT INTO dbo.[23_09Polzovateli] (login, parol, rol) VALUES ('teacher2', '12345', 'teacher');
    INSERT INTO dbo.[23_09Polzovateli] (login, parol, rol) VALUES ('student1', '12345', 'student');
    INSERT INTO dbo.[23_09Polzovateli] (login, parol, rol) VALUES ('student2', '12345', 'student');
    INSERT INTO dbo.[23_09Polzovateli] (login, parol, rol) VALUES ('student3', '12345', 'student');
    PRINT 'Созданы учётные записи';
END

SELECT @p_admin    = polzovatel_id FROM dbo.[23_09Polzovateli] WHERE login = 'admin1';
SELECT @p_teacher1 = polzovatel_id FROM dbo.[23_09Polzovateli] WHERE login = 'teacher1';
SELECT @p_teacher2 = polzovatel_id FROM dbo.[23_09Polzovateli] WHERE login = 'teacher2';
SELECT @p_student1 = polzovatel_id FROM dbo.[23_09Polzovateli] WHERE login = 'student1';
SELECT @p_student2 = polzovatel_id FROM dbo.[23_09Polzovateli] WHERE login = 'student2';
SELECT @p_student3 = polzovatel_id FROM dbo.[23_09Polzovateli] WHERE login = 'student3';

IF NOT EXISTS (SELECT 1 FROM dbo.[23_09Gruppy] WHERE nomer_klassa = 10 AND bukva_klassa = N'А')
BEGIN
    INSERT INTO dbo.[23_09Gruppy] (nomer_klassa, bukva_klassa) VALUES (10, N'А');
    INSERT INTO dbo.[23_09Gruppy] (nomer_klassa, bukva_klassa) VALUES (10, N'Б');
    INSERT INTO dbo.[23_09Gruppy] (nomer_klassa, bukva_klassa) VALUES (11, N'А');
    PRINT 'Созданы учебные группы';
END

SELECT @g10a = gruppa_id FROM dbo.[23_09Gruppy] WHERE nomer_klassa = 10 AND bukva_klassa = N'А';
SELECT @g10b = gruppa_id FROM dbo.[23_09Gruppy] WHERE nomer_klassa = 10 AND bukva_klassa = N'Б';
SELECT @g11a = gruppa_id FROM dbo.[23_09Gruppy] WHERE nomer_klassa = 11 AND bukva_klassa = N'А';

IF NOT EXISTS (SELECT 1 FROM dbo.[23_09Ucheniki] WHERE polzovatel_id = @p_student1)
BEGIN
    INSERT INTO dbo.[23_09Ucheniki] (polzovatel_id, gruppa_id, fio) VALUES (@p_student1, @g10a, N'Сидоров Пётр Петрович');
    INSERT INTO dbo.[23_09Ucheniki] (polzovatel_id, gruppa_id, fio) VALUES (@p_student2, @g10b, N'Петрова Анна Павловна');
    INSERT INTO dbo.[23_09Ucheniki] (polzovatel_id, gruppa_id, fio) VALUES (@p_student3, @g11a, N'Смирнов Алексей Алексеевич');
    PRINT 'Созданы записи об учениках';
END

IF NOT EXISTS (SELECT 1 FROM dbo.[23_09Prepodavateli] WHERE polzovatel_id = @p_teacher1)
BEGIN
    INSERT INTO dbo.[23_09Prepodavateli] (polzovatel_id, fio) VALUES (@p_teacher1, N'Петрова Анна Сергеевна');
    INSERT INTO dbo.[23_09Prepodavateli] (polzovatel_id, fio) VALUES (@p_teacher2, N'Иванов Иван Иванович');
    PRINT 'Созданы записи о преподавателях';
END

-- идентификаторы записей преподавателей (в курсах хранится именно этот ключ)
SELECT @pr1 = prepodavatel_id FROM dbo.[23_09Prepodavateli] WHERE polzovatel_id = @p_teacher1;
SELECT @pr2 = prepodavatel_id FROM dbo.[23_09Prepodavateli] WHERE polzovatel_id = @p_teacher2;

IF NOT EXISTS (SELECT 1 FROM dbo.[23_09Discipliny] WHERE nazvanie = N'Математика')
BEGIN
    INSERT INTO dbo.[23_09Discipliny] (nazvanie, opisanie) VALUES (N'Математика', N'Алгебра, геометрия, теория вероятностей');
    INSERT INTO dbo.[23_09Discipliny] (nazvanie, opisanie) VALUES (N'Информатика', N'Программирование, алгоритмы, базы данных');
    INSERT INTO dbo.[23_09Discipliny] (nazvanie, opisanie) VALUES (N'Физика', N'Механика, электричество, оптика');
    INSERT INTO dbo.[23_09Discipliny] (nazvanie, opisanie) VALUES (N'История России', N'Отечественная история и культура');
    PRINT 'Созданы дисциплины';
END

SELECT @d_math = disciplina_id FROM dbo.[23_09Discipliny] WHERE nazvanie = N'Математика';
SELECT @d_info = disciplina_id FROM dbo.[23_09Discipliny] WHERE nazvanie = N'Информатика';
SELECT @d_phys = disciplina_id FROM dbo.[23_09Discipliny] WHERE nazvanie = N'Физика';

IF NOT EXISTS (SELECT 1 FROM dbo.[23_09Kursy] WHERE nazvanie = N'Алгебра 10 класс')
BEGIN
    INSERT INTO dbo.[23_09Kursy] (disciplina_id, prepodavatel_id, nazvanie, opisanie)
    VALUES (@d_math, @pr1, N'Алгебра 10 класс', N'Изучение алгебраических выражений, уравнений и неравенств.');
    INSERT INTO dbo.[23_09Kursy] (disciplina_id, prepodavatel_id, nazvanie, opisanie)
    VALUES (@d_info, @pr2, N'Информатика 10 класс', N'Основы алгоритмизации и программирования на языке C#.');
    INSERT INTO dbo.[23_09Kursy] (disciplina_id, prepodavatel_id, nazvanie, opisanie)
    VALUES (@d_math, @pr1, N'Геометрия 10 класс', N'Треугольники, векторы, площади фигур.');
    INSERT INTO dbo.[23_09Kursy] (disciplina_id, prepodavatel_id, nazvanie, opisanie)
    VALUES (@d_phys, @pr1, N'Физика 11 класс', N'Механика, законы сохранения, электростатика.');
    PRINT 'Созданы учебные курсы';
END

SELECT @c_alg  = kurs_id FROM dbo.[23_09Kursy] WHERE nazvanie = N'Алгебра 10 класс';
SELECT @c_inf  = kurs_id FROM dbo.[23_09Kursy] WHERE nazvanie = N'Информатика 10 класс';
SELECT @c_geo  = kurs_id FROM dbo.[23_09Kursy] WHERE nazvanie = N'Геометрия 10 класс';
SELECT @c_phys = kurs_id FROM dbo.[23_09Kursy] WHERE nazvanie = N'Физика 11 класс';

IF NOT EXISTS (SELECT 1 FROM dbo.[23_09Zadaniya] WHERE nazvanie = N'Домашняя работа № 1')
BEGIN
    INSERT INTO dbo.[23_09Zadaniya] (kurs_id, nazvanie, opisanie, srok_sdachi, maks_ball)
    VALUES (@c_alg, N'Домашняя работа № 1', N'Решить задания 1-20 из учебника.', DATEADD(day, 7, GETDATE()), 100);
    INSERT INTO dbo.[23_09Zadaniya] (kurs_id, nazvanie, opisanie, srok_sdachi, maks_ball)
    VALUES (@c_alg, N'Контрольная работа № 1', N'Решение систем уравнений.', DATEADD(day, 3, GETDATE()), 50);
    INSERT INTO dbo.[23_09Zadaniya] (kurs_id, nazvanie, opisanie, srok_sdachi, maks_ball)
    VALUES (@c_inf, N'Практическая работа № 1', N'Разработать консольное приложение «Калькулятор».', DATEADD(day, 10, GETDATE()), 100);
    PRINT 'Созданы задания';
END

SELECT @z1 = zadanie_id FROM dbo.[23_09Zadaniya] WHERE nazvanie = N'Домашняя работа № 1';
SELECT @z2 = zadanie_id FROM dbo.[23_09Zadaniya] WHERE nazvanie = N'Контрольная работа № 1';
SELECT @z3 = zadanie_id FROM dbo.[23_09Zadaniya] WHERE nazvanie = N'Практическая работа № 1';

SELECT @u1 = uchenik_id FROM dbo.[23_09Ucheniki] WHERE polzovatel_id = @p_student1;
SELECT @u2 = uchenik_id FROM dbo.[23_09Ucheniki] WHERE polzovatel_id = @p_student2;
SELECT @u3 = uchenik_id FROM dbo.[23_09Ucheniki] WHERE polzovatel_id = @p_student3;

IF NOT EXISTS (SELECT 1 FROM dbo.[23_09ZapisNaKursy] WHERE kurs_id = @c_alg AND uchenik_id = @u1)
BEGIN
    INSERT INTO dbo.[23_09ZapisNaKursy] (kurs_id, uchenik_id, status) VALUES (@c_alg, @u1, 'aktivna');
    INSERT INTO dbo.[23_09ZapisNaKursy] (kurs_id, uchenik_id, status) VALUES (@c_inf, @u1, 'aktivna');
    INSERT INTO dbo.[23_09ZapisNaKursy] (kurs_id, uchenik_id, status) VALUES (@c_alg, @u2, 'aktivna');
    INSERT INTO dbo.[23_09ZapisNaKursy] (kurs_id, uchenik_id, status) VALUES (@c_geo, @u3, 'aktivna');
    PRINT 'Созданы записи на курсы';
END

IF NOT EXISTS (SELECT 1 FROM dbo.[23_09Vypolneniya] WHERE zadanie_id = @z1 AND uchenik_id = @u1)
BEGIN
    DECLARE @w1 int, @w2 int, @w3 int, @w4 int;
    INSERT INTO dbo.[23_09Vypolneniya] (zadanie_id, uchenik_id, otvet, data_sdachi, status)
    VALUES (@z1, @u1, N'Решение выполнено, все 20 заданий разобраны подробно.', DATEADD(hour, -30, GETDATE()), 'otpravleno');
    SET @w1 = SCOPE_IDENTITY();
    INSERT INTO dbo.[23_09Vypolneniya] (zadanie_id, uchenik_id, otvet, data_sdachi, status)
    VALUES (@z2, @u1, N'Системы решены методом сложения, получены корни x=3 и x=-5.', DATEADD(hour, -26, GETDATE()), 'provereno');
    SET @w2 = SCOPE_IDENTITY();
    INSERT INTO dbo.[23_09Vypolneniya] (zadanie_id, uchenik_id, otvet, data_sdachi, status)
    VALUES (@z3, @u1, N'Калькулятор реализован, разбор выражений через стек.', DATEADD(hour, -20, GETDATE()), 'provereno');
    SET @w3 = SCOPE_IDENTITY();
    INSERT INTO dbo.[23_09Vypolneniya] (zadanie_id, uchenik_id, otvet, data_sdachi, status)
    VALUES (@z1, @u2, N'Часть заданий не выполнена, остальное решено.', DATEADD(hour, -22, GETDATE()), 'provereno');
    SET @w4 = SCOPE_IDENTITY();

    INSERT INTO dbo.[23_09Ocenki] (vypolnenie_id, ball, kommentariy) VALUES (@w2, 45, N'Отличная работа, все примеры решены верно.');
    INSERT INTO dbo.[23_09Ocenki] (vypolnenie_id, ball, kommentariy) VALUES (@w3, 85, N'Хорошая работа, код оформлен понятно.');
    INSERT INTO dbo.[23_09Ocenki] (vypolnenie_id, ball, kommentariy) VALUES (@w4, 32, N'Есть замечания по третьему и седьмому заданиям, разберите их.');
    PRINT 'Созданы работы и оценки';
END
GO

PRINT '============================================';
PRINT ' База данных user87_db развёрнута';
PRINT ' Пользователи: admin1, teacher1, teacher2,';
PRINT '               student1, student2, student3';
PRINT ' Пароль всех пользователей: 12345';
PRINT '============================================';
GO
