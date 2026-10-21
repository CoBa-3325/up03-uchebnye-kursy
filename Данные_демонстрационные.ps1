<#
    Скрипт подготовки демонстрационных данных для информационной системы "Учебные курсы".
    Использование:
        powershell -ExecutionPolicy Bypass -File seed-demo.ps1 -Action Seed   # заполнить
        powershell -ExecutionPolicy Bypass -File seed-demo.ps1 -Action Undo   # удалить
    Пароли всех создаваемых пользователей: 12345 (хранятся в виде PBKDF2-хеша).
#>
param(
    [ValidateSet('Seed', 'Undo', 'Status')] [string]$Action = 'Seed',
    [string]$ConnectionString = '',
    [string]$ConfigFile = ''
)

$ErrorActionPreference = 'Stop'

# Строка подключения берётся из локального файла App.config.local рядом с
# приложением (он не входит в репозиторий) либо передаётся параметром.
if ($ConnectionString -eq '') {
    if ($ConfigFile -eq '') {
        $ConfigFile = Join-Path $PSScriptRoot 'Матулов УП03\WpfApp1\WpfApp1\App.config.local'
        if (-not (Test-Path $ConfigFile)) {
            $ConfigFile = Join-Path $PSScriptRoot 'Матулов УП03\WpfApp1\WpfApp1\bin\Debug\App.config.local'
        }
    }
    if (Test-Path $ConfigFile) {
        $xml = [xml](Get-Content $ConfigFile -Raw)
        $node = $xml.SelectSingleNode('/configuration/connectionStrings/add[@name="DefaultConnection"]')
        if ($node) { $ConnectionString = $node.connectionString }
    }
}
if ($ConnectionString -eq '') {
    Write-Host 'Строка подключения не задана.'
    Write-Host 'Укажите её параметром -ConnectionString либо создайте файл App.config.local:'
    Write-Host '  <configuration><connectionStrings><add name="DefaultConnection" connectionString="..." providerName="System.Data.SqlClient" /></connectionStrings></configuration>'
    exit 1
}

$cs = $ConnectionString
$conn = New-Object System.Data.SqlClient.SqlConnection $cs
$conn.Open()

function Invoke-Sql([string]$sql, [hashtable]$p = @{}) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    foreach ($k in $p.Keys) {
        $cmd.Parameters.AddWithValue($k, $p[$k]) | Out-Null
    }
    $cmd.ExecuteNonQuery()
}

function Get-Scalar([string]$sql, [hashtable]$p = @{}) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    foreach ($k in $p.Keys) {
        $cmd.Parameters.AddWithValue($k, $p[$k]) | Out-Null
    }
    $cmd.ExecuteScalar()
}

function Get-Hash([string]$plain) {
    $salt = New-Object byte[] 16
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
    $dk = New-Object System.Security.Cryptography.Rfc2898DeriveBytes(
        $plain, $salt, 100000, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $hash = $dk.GetBytes(32)
    return 'PBKDF2$100000$' + [Convert]::ToBase64String($salt) + '$' + [Convert]::ToBase64String($hash)
}

# --- справочники ---
function Ensure-Group([int]$nomer, [string]$bukva) {
    $id = Get-Scalar "SELECT [gruppa_id] FROM [23_09Gruppy] WHERE [nomer_klassa]=@n AND [bukva_klassa]=@b" @{ '@n' = $nomer; '@b' = $bukva }
    if ($null -eq $id) {
        Invoke-Sql "INSERT INTO [23_09Gruppy] ([nomer_klassa],[bukva_klassa]) VALUES (@n,@b)" @{ '@n' = $nomer; '@b' = $bukva } | Out-Null
        $id = Get-Scalar "SELECT [gruppa_id] FROM [23_09Gruppy] WHERE [nomer_klassa]=@n AND [bukva_klassa]=@b" @{ '@n' = $nomer; '@b' = $bukva }
        Write-Host "  + группа $nomer$bukva (id=$id)"
    }
    return [int]$id
}

function Ensure-Disc([string]$name, [string]$opis) {
    $id = Get-Scalar "SELECT [disciplina_id] FROM [23_09Discipliny] WHERE [nazvanie]=@n" @{ '@n' = $name }
    if ($null -eq $id) {
        Invoke-Sql "INSERT INTO [23_09Discipliny] ([nazvanie],[opisanie]) VALUES (@n,@o)" @{ '@n' = $name; '@o' = $opis } | Out-Null
        $id = Get-Scalar "SELECT [disciplina_id] FROM [23_09Discipliny] WHERE [nazvanie]=@n" @{ '@n' = $name }
        Write-Host "  + дисциплина '$name' (id=$id)"
    }
    return [int]$id
}

function Ensure-User([string]$login, [string]$rol, [string]$fio, [string]$gruppaId) {
    $id = Get-Scalar "SELECT [polzovatel_id] FROM [23_09Polzovateli] WHERE [login]=@l" @{ '@l' = $login }
    if ($null -ne $id) { return [int]$id }

    Invoke-Sql "INSERT INTO [23_09Polzovateli] ([login],[parol],[rol]) VALUES (@l,@p,@r)" `
        @{ '@l' = $login; '@p' = (Get-Hash '12345'); '@r' = $rol } | Out-Null
    $id = Get-Scalar "SELECT [polzovatel_id] FROM [23_09Polzovateli] WHERE [login]=@l" @{ '@l' = $login }

    if ($rol -eq 'student') {
        Invoke-Sql "INSERT INTO [23_09Ucheniki] ([polzovatel_id],[gruppa_id],[fio]) VALUES (@u,@g,@f)" `
            @{ '@u' = [int]$id; '@g' = [int]$gruppaId; '@f' = $fio } | Out-Null
    } elseif ($rol -eq 'teacher') {
        Invoke-Sql "INSERT INTO [23_09Prepodavateli] ([polzovatel_id],[fio]) VALUES (@u,@f)" `
            @{ '@u' = [int]$id; '@f' = $fio } | Out-Null
    }
    Write-Host "  + пользователь $login ($rol)"
    return [int]$id
}

function Ensure-Prep([int]$userId) {
    $id = Get-Scalar "SELECT [prepodavatel_id] FROM [23_09Prepodavateli] WHERE [polzovatel_id]=@u" @{ '@u' = $userId }
    return [int]$id
}

function Ensure-Course([int]$discId, [int]$prepId, [string]$name, [string]$opis) {
    $id = Get-Scalar "SELECT [kurs_id] FROM [23_09Kursy] WHERE [nazvanie]=@n" @{ '@n' = $name }
    if ($null -eq $id) {
        Invoke-Sql "INSERT INTO [23_09Kursy] ([disciplina_id],[prepodavatel_id],[nazvanie],[opisanie]) VALUES (@d,@p,@n,@o)" `
            @{ '@d' = $discId; '@p' = $prepId; '@n' = $name; '@o' = $opis } | Out-Null
        $id = Get-Scalar "SELECT [kurs_id] FROM [23_09Kursy] WHERE [nazvanie]=@n" @{ '@n' = $name }
        Write-Host "  + курс '$name' (id=$id)"
    }
    return [int]$id
}

function Ensure-Task([int]$kursId, [string]$name, [string]$opis, [int]$srokOffsetDays, [int]$maks) {
    $id = Get-Scalar "SELECT [zadanie_id] FROM [23_09Zadaniya] WHERE [kurs_id]=@k AND [nazvanie]=@n" @{ '@k' = $kursId; '@n' = $name }
    if ($null -eq $id) {
        Write-Host ("  DEBUG: kursId={0} name='{1}' maks={2} ({3}) d={4}" -f $kursId, $name, $maks, $maks.GetType().Name, $srokOffsetDays)
        Invoke-Sql "INSERT INTO [23_09Zadaniya] ([kurs_id],[nazvanie],[opisanie],[srok_sdachi],[maks_ball]) VALUES (@k,@n,@o,DATEADD(day,@d,GETDATE()),@m)" `
            @{ '@k' = $kursId; '@n' = $name; '@o' = $opis; '@d' = $srokOffsetDays; '@m' = $maks } | Out-Null
        $id = Get-Scalar "SELECT [zadanie_id] FROM [23_09Zadaniya] WHERE [kurs_id]=@k AND [nazvanie]=@n" @{ '@k' = $kursId; '@n' = $name }
        Write-Host "  + задание '$name' (id=$id)"
    }
    return [int]$id
}

function Ensure-Enroll([int]$kursId, [int]$uchenikId) {
    $n = Get-Scalar "SELECT COUNT(*) FROM [23_09ZapisNaKursy] WHERE [kurs_id]=@k AND [uchenik_id]=@u" @{ '@k' = $kursId; '@u' = $uchenikId }
    if ($n -eq 0) {
        Invoke-Sql "INSERT INTO [23_09ZapisNaKursy] ([kurs_id],[uchenik_id],[status]) VALUES (@k,@u,'aktivna')" @{ '@k' = $kursId; '@u' = $uchenikId } | Out-Null
        Write-Host "  + запись на курс (kurs=$kursId, uchenik=$uchenikId)"
    }
}

function Ensure-Work([int]$zadanieId, [int]$uchenikId, [string]$otvet, [string]$status, $ball, $komment) {
    $id = Get-Scalar "SELECT [vypolnenie_id] FROM [23_09Vypolneniya] WHERE [zadanie_id]=@z AND [uchenik_id]=@u" @{ '@z' = $zadanieId; '@u' = $uchenikId }
    if ($null -ne $id) { return [int]$id }
    Invoke-Sql "INSERT INTO [23_09Vypolneniya] ([zadanie_id],[uchenik_id],[otvet],[data_sdachi],[status]) VALUES (@z,@u,@o,DATEADD(hour,-@h,GETDATE()),@s)" `
        @{ '@z' = $zadanieId; '@u' = $uchenikId; '@o' = $otvet; '@h' = -30; '@s' = $status } | Out-Null
    $id = Get-Scalar "SELECT [vypolnenie_id] FROM [23_09Vypolneniya] WHERE [zadanie_id]=@z AND [uchenik_id]=@u" @{ '@z' = $zadanieId; '@u' = $uchenikId }
    if ($null -ne $ball) {
        Invoke-Sql "INSERT INTO [23_09Ocenki] ([vypolnenie_id],[ball],[kommentariy]) VALUES (@v,@b,@k)" `
            @{ '@v' = [int]$id; '@b' = [int]$ball; '@k' = $komment } | Out-Null
    }
    Write-Host "  + работа (zadanie=$zadanieId, uchenik=$uchenikId, status=$status)"
    return [int]$id
}

switch ($Action) {

    'Status' {
        Write-Host "--- Пользователи ---"
        $r = $conn.CreateCommand()
        $r.CommandText = "SELECT p.[polzovatel_id], p.[login], p.[rol], p.[aktivnost], u.[fio], g.[nomer_klassa], g.[bukva_klassa] FROM [23_09Polzovateli] p LEFT JOIN [23_09Ucheniki] u ON p.[polzovatel_id]=u.[polzovatel_id] LEFT JOIN [23_09Gruppy] g ON u.[gruppa_id]=g.[gruppa_id] LEFT JOIN [23_09Prepodavateli] pr ON p.[polzovatel_id]=pr.[polzovatel_id] ORDER BY p.[polzovatel_id]"
        $reader = $r.ExecuteReader()
        $dt = New-Object System.Data.DataTable; $dt.Load($reader)
        $reader.Close()
        $dt | Format-Table -AutoSize | Out-String -Width 160
        foreach ($t in '23_09Gruppy','23_09Discipliny','23_09Prepodavateli','23_09Kursy','23_09Zadaniya','23_09ZapisNaKursy','23_09Vypolneniya','23_09Ocenki') {
            "{0,-24} {1}" -f $t, (Get-Scalar "SELECT COUNT(*) FROM [$t]")
        }
    }

    'Seed' {
        Write-Host "== Подготовка демонстрационных данных =="
        $g10a = Ensure-Group 10 'А'
        $g10b = Ensure-Group 10 'Б'
        $g11a = Ensure-Group 11 'А'

        $dMath = Ensure-Disc 'Математика' 'Алгебра, геометрия, теория вероятностей'
        $dInfo = Ensure-Disc 'Информатика' 'Программирование, алгоритмы, базы данных'
        $dPhys = Ensure-Disc 'Физика' 'Механика, электричество, оптика'
        $dHist = Ensure-Disc 'История России' 'Отечественная история и культура'

        $uTeacher1 = Ensure-User 'teacher1' 'teacher' 'Петрова Анна Сергеевна' 0
        $uTeacher2 = Ensure-User 'teacher2' 'teacher' 'Иванов Иван Иванович' 0
        $uStud1 = Ensure-User 'student1' 'student' 'Сидоров Пётр Петрович' $g10a
        $uStud2 = Ensure-User 'student2' 'student' 'Петрова Анна Павловна' $g10b
        $uStud3 = Ensure-User 'student3' 'student' 'Смирнов Алексей Алексеевич' $g11a

        $p1 = Ensure-Prep $uTeacher1
        $p2 = Ensure-Prep $uTeacher2

        $cAlg = Ensure-Course $dMath $p1 'Алгебра 10 класс' 'Изучение алгебраических выражений, уравнений и неравенств.'
        $cInf = Ensure-Course $dInfo $p2 'Информатика 10 класс' 'Основы алгоритмизации и программирования на языке C#.'
        $cGeo = Ensure-Course $dMath $p1 'Геометрия 10 класс' 'Треугольники, векторы, площади фигур.'
        $cPh  = Ensure-Course $dPhys $p1 'Физика 11 класс' 'Механика, законы сохранения, электростатика.'

        $tAlg1 = Ensure-Task $cAlg 'Домашняя работа № 1' 'Решить задания 1-20 из учебника.' 7 100
        $tAlg2 = Ensure-Task $cAlg 'Контрольная работа № 1' 'Решение систем уравнений.' 3 50
        $tInf1 = Ensure-Task $cInf 'Практическая работа № 1' 'Разработать консольное приложение «Калькулятор».' 10 100

        $uch1 = Get-Scalar "SELECT [uchenik_id] FROM [23_09Ucheniki] WHERE [polzovatel_id]=@u" @{ '@u' = $uStud1 }
        $uch2 = Get-Scalar "SELECT [uchenik_id] FROM [23_09Ucheniki] WHERE [polzovatel_id]=@u" @{ '@u' = $uStud2 }
        $uch3 = Get-Scalar "SELECT [uchenik_id] FROM [23_09Ucheniki] WHERE [polzovatel_id]=@u" @{ '@u' = $uStud3 }

        Ensure-Enroll $cAlg $uch1
        Ensure-Enroll $cInf $uch1
        Ensure-Enroll $cGeo $uch3
        Ensure-Enroll $cAlg $uch2

        Ensure-Work $tAlg1 $uch1 'Решение выполнено, все 20 заданий разобраны подробно.' 'otpravleno' $null $null
        Ensure-Work $tAlg2 $uch1 'Системы решены методом сложения, получены корни x=3 и x=-5.' 'provereno' 45 'Отличная работа, все примеры решены верно.'
        Ensure-Work $tInf1 $uch1 'Калькулятор реализован, разбор выражений через стек.' 'provereno' 85 'Хорошая работа, код оформлен понятно.'
        Ensure-Work $tAlg1 $uch2 'Часть заданий не выполнена, остальное решено.' 'provereno' 32 'Есть замечания по третьему и седьмому заданиям, разберите их.'

        Write-Host ""
        Write-Host "Готово. Учётные записи: admin1 / teacher1 / teacher2 / student1 / student2 / student3, пароль 12345"
    }

    'Undo' {
        Write-Host "== Удаление демонстрационных данных =="
        Invoke-Sql @"
DELETE FROM [23_09Ocenki] WHERE [vypolnenie_id] IN (SELECT v.[vypolnenie_id] FROM [23_09Vypolneniya] v
    INNER JOIN [23_09Ucheniki] u ON v.[uchenik_id]=u.[uchenik_id]
    INNER JOIN [23_09Polzovateli] p ON u.[polzovatel_id]=p.[polzovatel_id]
    WHERE p.[login] IN ('student2','student3'))
"@
        Invoke-Sql @"
DELETE FROM [23_09Vypolneniya] WHERE [uchenik_id] IN (SELECT u.[uchenik_id] FROM [23_09Ucheniki] u
    INNER JOIN [23_09Polzovateli] p ON u.[polzovatel_id]=p.[polzovatel_id]
    WHERE p.[login] IN ('student2','student3'))
"@
        Invoke-Sql @"
DELETE FROM [23_09ZapisNaKursy] WHERE [uchenik_id] IN (SELECT u.[uchenik_id] FROM [23_09Ucheniki] u
    INNER JOIN [23_09Polzovateli] p ON u.[polzovatel_id]=p.[polzovatel_id]
    WHERE p.[login] IN ('student2','student3'))
"@
        Invoke-Sql @"
DELETE FROM [23_09Ucheniki] WHERE [polzovatel_id] IN (SELECT [polzovatel_id] FROM [23_09Polzovateli] WHERE [login] IN ('student2','student3'))
"@
        Invoke-Sql @"
DELETE FROM [23_09Prepodavateli] WHERE [polzovatel_id] IN (SELECT [polzovatel_id] FROM [23_09Polzovateli] WHERE [login] = 'teacher2')
"@
        Invoke-Sql "DELETE FROM [23_09Polzovateli] WHERE [login] IN ('teacher2','student2','student3')" | Out-Null
        Invoke-Sql "DELETE FROM [23_09Kursy] WHERE [nazvanie] IN ('Информатика 10 класс','Геометрия 10 класс','Физика 11 класс')" | Out-Null
        Invoke-Sql "DELETE FROM [23_09Zadaniya] WHERE [nazvanie] IN ('Практическая работа № 1')" | Out-Null
        Invoke-Sql "DELETE FROM [23_09Discipliny] WHERE [nazvanie] IN ('Информатика','Физика','История России')" | Out-Null
        Invoke-Sql "DELETE FROM [23_09Gruppy] WHERE [gruppa_id] > 1" | Out-Null
        Write-Host "Готово. Исходное состояние (3 пользователя, 1 группа, 1 дисциплина, 1 преподаватель) восстановлено."
    }
}

$conn.Close()
