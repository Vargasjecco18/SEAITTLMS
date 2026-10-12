using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

/// <summary>Where the backup files are kept ("Backup" section of appsettings.json).</summary>
public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>A folder. A relative one is inside the folder of the app (next to appsettings.json).</summary>
    public string Directory { get; set; } = "Backups";
}

/// <summary>One backup file in the folder.</summary>
/// <param name="Kind">"manual", "auto", "before-restore" or "uploaded".</param>
/// <param name="Valid">Whether it is a backup made by this system (the first lines say so).</param>
public sealed record BackupInfo(string FileName, long SizeBytes, DateTime CreatedAtUtc, string Kind, string? Migration, int Tables, bool Valid)
{
    public string KindText => Kind switch
    {
        "auto" => "Automatic",
        "before-restore" => "Before a restore",
        "uploaded" => "Uploaded",
        _ => "Manual",
    };
}

public sealed record DatabaseSummary(string DatabaseName, string ServerVersion, int Tables, long Rows, long SizeBytes, string? LastMigration);

public sealed record BackupResult(bool Succeeded, string? FileName, string? Error);

/// <summary>
/// Backing up and restoring the whole database. A backup is a plain text file of SQL (the tables and all their rows) that the system writes
/// and reads by itself, so nothing else needs to be installed (no mysqldump). It is saved in the Backups folder and can be downloaded. The
/// files people keep in their lockers and the pictures are in the database, so they are in the backup too.
/// Restoring replaces everything with what is in the file; before that a backup of the current data is made, and if the restore fails that
/// backup is put back.
/// </summary>
public sealed class BackupService(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<BackupOptions> options,
    IHostEnvironment environment,
    TimeProvider clock,
    SystemSettingsService settings,
    ProfilePhotoService photos,
    SecurityLogService securityLog,
    ILogger<BackupService> logger)
{
    public const string Marker = "-- SEAITT-LMS-BACKUP 1";

    private static readonly Regex NamePattern = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,100}\.sql$", RegexOptions.Compiled);

    // One backup or restore at a time.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>The folder with the backups (made when it is first needed).</summary>
    public string Directory
    {
        get
        {
            var path = options.Value.Directory;
            var full = Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path);
            System.IO.Directory.CreateDirectory(full);
            return full;
        }
    }

    /// <summary>True when the name is one a backup file can have (so a name from a web address can't reach another folder).</summary>
    public static bool IsValidName(string? name) => name is not null && NamePattern.IsMatch(name) && Path.GetFileName(name) == name;

    public string? PathOf(string name) => IsValidName(name) && File.Exists(Path.Combine(Directory, name)) ? Path.Combine(Directory, name) : null;

    // ======================================================================= list

    public IReadOnlyList<BackupInfo> List()
    {
        var folder = new DirectoryInfo(Directory);
        var result = new List<BackupInfo>();
        foreach (var file in folder.EnumerateFiles("*.sql"))
        {
            if (!IsValidName(file.Name))
            {
                continue;
            }

            var (valid, migration, tables, created) = ReadHeader(file.FullName);
            result.Add(new BackupInfo(file.Name, file.Length, created ?? file.LastWriteTimeUtc, KindOf(file.Name), migration, tables, valid));
        }

        return result.OrderByDescending(b => b.CreatedAtUtc).ThenByDescending(b => b.FileName).ToList();
    }

    private static string KindOf(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("-auto")) return "auto";
        if (lower.Contains("-before-restore")) return "before-restore";
        if (lower.Contains("uploaded")) return "uploaded";
        return "manual";
    }

    /// <summary>Reads the first lines of a file: is it a backup of ours, which migration was the database at, how many tables, when it was made.</summary>
    private static (bool Valid, string? Migration, int Tables, DateTime? Created) ReadHeader(string path)
    {
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            if (reader.ReadLine() != Marker)
            {
                return (false, null, 0, null);
            }

            string? migration = null;
            DateTime? created = null;
            var tables = 0;
            for (var i = 0; i < 12; i++)
            {
                var line = reader.ReadLine();
                if (line is null || !line.StartsWith("--"))
                {
                    break;
                }

                if (line.StartsWith("-- Migration: ", StringComparison.Ordinal))
                {
                    migration = line["-- Migration: ".Length..].Trim();
                }
                else if (line.StartsWith("-- Tables: ", StringComparison.Ordinal) && int.TryParse(line["-- Tables: ".Length..], out var n))
                {
                    tables = n;
                }
                else if (line.StartsWith("-- Created: ", StringComparison.Ordinal)
                    && DateTime.TryParse(line["-- Created: ".Length..], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when))
                {
                    created = DateTime.SpecifyKind(when, DateTimeKind.Utc);
                }
            }

            return (true, migration, tables, created);
        }
        catch (IOException)
        {
            return (false, null, 0, null);
        }
    }

    public async Task<DatabaseSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var conn = (MySqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DATABASE(), VERSION(), COUNT(*), COALESCE(SUM(TABLE_ROWS), 0), COALESCE(SUM(DATA_LENGTH + INDEX_LENGTH), 0)
            FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        var summary = new DatabaseSummary(
            reader.GetString(0), reader.GetString(1), Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
            Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture), Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture), null);
        await reader.CloseAsync();

        var migration = (await db.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault();
        return summary with { LastMigration = migration };
    }

    // ======================================================================= make a backup

    /// <summary>Makes a backup. <paramref name="kind"/> is "manual" or "auto" (in the file's name); the person is told in the security log.</summary>
    public async Task<BackupResult> CreateAsync(string kind, int? actingUserId, CancellationToken ct = default)
    {
        if (actingUserId is { } actor && !await IsAdminAsync(actor, ct))
        {
            return new BackupResult(false, null, "Only an administrator can make a backup.");
        }

        await Gate.WaitAsync(ct);
        try
        {
            var name = await WriteBackupAsync(kind, ct);
            await securityLog.AddAsync(SecurityEvents.BackupCreated, null, details: $"{(kind == "auto" ? "Automatic backup" : "Backup")} {name} was made.", actorUserId: actingUserId, ct: ct);
            return new BackupResult(true, name, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The backup could not be made.");
            return new BackupResult(false, null, "The backup could not be made: " + ex.GetBaseException().Message);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<string> WriteBackupAsync(string kind, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var suffix = kind switch { "auto" => "-auto", "before-restore" => "-before-restore", _ => "" };
        var name = NewName(now, suffix);
        var final = Path.Combine(Directory, name);
        var temp = final + ".tmp";

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var conn = (MySqlConnection)db.Database.GetDbConnection();
            await conn.OpenAsync(ct);

            // Everything is read inside one transaction, so the file is one moment of the database even while people use it.
            await using (var begin = conn.CreateCommand())
            {
                begin.CommandText = "START TRANSACTION WITH CONSISTENT SNAPSHOT";
                await begin.ExecuteNonQueryAsync(ct);
            }

            var tables = new List<string>();
            await using (var list = conn.CreateCommand())
            {
                list.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME";
                await using var r = await list.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    tables.Add(r.GetString(0));
                }
            }

            string? migration = null;
            if (tables.Contains("__EFMigrationsHistory"))
            {
                await using var m = conn.CreateCommand();
                m.CommandText = "SELECT migration_id FROM `__EFMigrationsHistory` ORDER BY migration_id DESC LIMIT 1";
                migration = (await m.ExecuteScalarAsync(ct)) as string;
            }

            string databaseName, version;
            await using (var info = conn.CreateCommand())
            {
                info.CommandText = "SELECT DATABASE(), VERSION()";
                await using var r = await info.ExecuteReaderAsync(ct);
                await r.ReadAsync(ct);
                databaseName = r.GetString(0);
                version = r.GetString(1);
            }

            await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n" })
            {
                await writer.WriteLineAsync(Marker);
                await writer.WriteLineAsync($"-- Created: {now:yyyy-MM-ddTHH:mm:ssZ}");
                await writer.WriteLineAsync($"-- Database: {databaseName}");
                await writer.WriteLineAsync($"-- Server: {version}");
                await writer.WriteLineAsync($"-- Migration: {migration}");
                await writer.WriteLineAsync($"-- Tables: {tables.Count}");
                await writer.WriteLineAsync("-- A backup of the SEAITT e-LMS database. Restore it from System > Database Backup & Restore.");
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("SET NAMES utf8mb4;");
                await writer.WriteLineAsync("SET FOREIGN_KEY_CHECKS = 0;");
                await writer.WriteLineAsync("SET UNIQUE_CHECKS = 0;");
                await writer.WriteLineAsync("SET SQL_MODE = '';");
                await writer.WriteLineAsync();

                foreach (var table in tables)
                {
                    await WriteTableAsync(conn, writer, table, ct);
                }

                await writer.WriteLineAsync("SET FOREIGN_KEY_CHECKS = 1;");
                await writer.WriteLineAsync("SET UNIQUE_CHECKS = 1;");
                await writer.WriteLineAsync("-- End of the backup");
            }

            File.Move(temp, final, overwrite: false);
            return name;
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <summary>A name for a new file in the folder. Two backups in the same second (a restore makes its own first) get different names.</summary>
    private string NewName(DateTime now, string suffix)
    {
        var name = $"seaittlms-{now:yyyyMMdd-HHmmss}{suffix}.sql";
        for (var n = 2; File.Exists(Path.Combine(Directory, name)); n++)
        {
            name = $"seaittlms-{now:yyyyMMdd-HHmmss}{suffix}-{n}.sql";
        }

        return name;
    }

    private static async Task WriteTableAsync(MySqlConnection conn, StreamWriter writer, string table, CancellationToken ct)
    {
        var quoted = Quote(table);
        await writer.WriteLineAsync($"-- Table {table}");
        await writer.WriteLineAsync($"DROP TABLE IF EXISTS {quoted};");

        await using (var create = conn.CreateCommand())
        {
            create.CommandText = $"SHOW CREATE TABLE {quoted}";
            await using var r = await create.ExecuteReaderAsync(ct);
            await r.ReadAsync(ct);
            await writer.WriteLineAsync(r.GetString(1) + ";");
        }

        await using var select = conn.CreateCommand();
        select.CommandText = $"SELECT * FROM {quoted}";
        await using var rows = await select.ExecuteReaderAsync(ct);

        var columns = Enumerable.Range(0, rows.FieldCount).Select(i => Quote(rows.GetName(i))).ToList();
        var header = $"INSERT INTO {quoted} ({string.Join(", ", columns)}) VALUES";
        var line = new StringBuilder();
        var count = 0;

        // Many rows in one statement (faster to restore), but not too many: a statement is at most about 1 MB, unless a single row is bigger.
        while (await rows.ReadAsync(ct))
        {
            if (count == 0)
            {
                line.Append(header).Append('\n');
            }
            else
            {
                line.Append(",\n");
            }

            line.Append('(');
            for (var i = 0; i < rows.FieldCount; i++)
            {
                if (i > 0)
                {
                    line.Append(',');
                }

                AppendLiteral(line, rows.IsDBNull(i) ? null : rows.GetValue(i));
            }

            line.Append(')');
            count++;

            if (count >= 200 || line.Length > 1_000_000)
            {
                line.Append(";\n");
                await writer.WriteAsync(line.ToString());
                line.Clear();
                count = 0;
            }
        }

        if (count > 0)
        {
            line.Append(";\n");
            await writer.WriteAsync(line.ToString());
        }

        await writer.WriteLineAsync();
    }

    private static string Quote(string identifier) => "`" + identifier.Replace("`", "``") + "`";

    /// <summary>Writes one value the way SQL reads it back: text in quotes (with the special characters escaped), bytes as 0x..., dates as text.</summary>
    internal static void AppendLiteral(StringBuilder sb, object? value)
    {
        switch (value)
        {
            case null:
                sb.Append("NULL");
                break;
            case bool b:
                sb.Append(b ? '1' : '0');
                break;
            case byte[] bytes:
                if (bytes.Length == 0)
                {
                    sb.Append("''");
                }
                else
                {
                    sb.Append("0x").Append(Convert.ToHexString(bytes));
                }

                break;
            case string text:
                AppendString(sb, text);
                break;
            case DateTime dt:
                sb.Append('\'').Append(dt.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture)).Append('\'');
                break;
            case DateOnly d:
                sb.Append('\'').Append(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('\'');
                break;
            case TimeSpan t:
                sb.Append('\'').Append(t < TimeSpan.Zero ? "-" : "").Append(t.Duration().ToString(@"hh\:mm\:ss\.ffffff", CultureInfo.InvariantCulture)).Append('\'');
                break;
            case decimal m:
                sb.Append(m.ToString(CultureInfo.InvariantCulture));
                break;
            case double dbl:
                sb.Append(dbl.ToString("R", CultureInfo.InvariantCulture));
                break;
            case float f:
                sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
                break;
            case sbyte or byte or short or ushort or int or uint or long or ulong:
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
            default:
                AppendString(sb, Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");
                break;
        }
    }

    private static void AppendString(StringBuilder sb, string text)
    {
        sb.Append('\'');
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\'': sb.Append("\\'"); break;
                case '\0': sb.Append("\\0"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\u001a': sb.Append("\\Z"); break;
                default: sb.Append(c); break;
            }
        }

        sb.Append('\'');
    }

    // ======================================================================= delete, keep, save an uploaded file

    public async Task<ServiceResult> DeleteAsync(int actingUserId, string name, CancellationToken ct = default)
    {
        if (!await IsAdminAsync(actingUserId, ct))
        {
            return ServiceResult.Fail("Only an administrator can delete a backup.");
        }

        var path = PathOf(name);
        if (path is null)
        {
            return ServiceResult.Fail("This backup is no longer there.");
        }

        File.Delete(path);
        await securityLog.AddAsync(SecurityEvents.BackupDeleted, null, details: $"Backup {name} was deleted.", actorUserId: actingUserId, ct: ct);
        return ServiceResult.Ok($"Deleted {name}.");
    }

    /// <summary>Keeps the newest <paramref name="keep"/> automatic backups and deletes the older ones.</summary>
    public int PruneAutomatic(int keep)
    {
        var old = List().Where(b => b.Kind == "auto").Skip(Math.Max(1, keep)).ToList();
        foreach (var backup in old)
        {
            try
            {
                File.Delete(Path.Combine(Directory, backup.FileName));
            }
            catch (IOException)
            {
                // Taken away or in use: the next time.
            }
        }

        return old.Count;
    }

    /// <summary>Keeps a file the administrator sent. It must be a backup made by this system. Gives back its name in the folder.</summary>
    public async Task<(string? Name, string? Error)> SaveUploadedAsync(int actingUserId, Stream content, CancellationToken ct = default)
    {
        if (!await IsAdminAsync(actingUserId, ct))
        {
            return (null, "Only an administrator can upload a backup.");
        }

        var name = NewName(clock.GetUtcNow().UtcDateTime, "-uploaded");
        var path = Path.Combine(Directory, name);
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(file, ct);
        }

        var (valid, migration, _, _) = ReadHeader(path);
        if (!valid)
        {
            File.Delete(path);
            return (null, "That file is not a backup made by this system.");
        }

        if (migration is not null && !KnownMigrations().Contains(migration))
        {
            File.Delete(path);
            return (null, "That backup is from a newer version of the system than this one. Update the system first.");
        }

        return (name, null);
    }

    private HashSet<string> KnownMigrations()
    {
        using var db = dbFactory.CreateDbContext();
        return db.Database.GetMigrations().ToHashSet();
    }

    // ======================================================================= restore

    /// <summary>
    /// Replaces the whole database with what is in the backup. A backup of the data now is made first; if the restore fails, that backup is put
    /// back. While it runs, only administrators can use the system. It is not stopped when the page that started it is closed.
    /// </summary>
    public async Task<ServiceResult> RestoreAsync(int actingUserId, string name)
    {
        if (!await IsAdminAsync(actingUserId, CancellationToken.None))
        {
            return ServiceResult.Fail("Only an administrator can restore a backup.");
        }

        var path = PathOf(name);
        if (path is null)
        {
            return ServiceResult.Fail("This backup is no longer there.");
        }

        var (valid, migration, _, _) = ReadHeader(path);
        if (!valid)
        {
            return ServiceResult.Fail("This file is not a backup made by this system.");
        }

        if (migration is not null && !KnownMigrations().Contains(migration))
        {
            return ServiceResult.Fail("This backup is from a newer version of the system than this one. Update the system first.");
        }

        await Gate.WaitAsync();
        string? safety = null;
        try
        {
            settings.BeginRestore();

            // 1. What is there now is saved first.
            safety = await WriteBackupAsync("before-restore", CancellationToken.None);

            // 2. The file is read once to check it (only the statements a backup has), then run.
            var tables = CheckScript(path);

            try
            {
                await RunScriptAsync(path, tables);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The restore of {Backup} failed; the data from before it is put back.", name);
                string rollback;
                try
                {
                    await RunScriptAsync(Path.Combine(Directory, safety), CheckScript(Path.Combine(Directory, safety)));
                    rollback = "Everything is as it was before: the data from before the restore was put back.";
                }
                catch (Exception back)
                {
                    logger.LogError(back, "The data from before the restore could not be put back.");
                    rollback = $"The data could not be put back by itself. Restore {safety} to get back what was there before.";
                }

                await TryAfterRestoreAsync();
                await securityLog.AddAsync(SecurityEvents.BackupRestoreFailed, null, details: $"Restoring {name} failed: {ex.GetBaseException().Message}", actorUserId: actingUserId);
                return ServiceResult.Fail($"The restore failed: {ex.GetBaseException().Message}", rollback);
            }

            await TryAfterRestoreAsync();
            await securityLog.AddAsync(SecurityEvents.BackupRestored, null, details: $"The database was restored from {name}. The data from before is in {safety}.", actorUserId: actingUserId);
            return ServiceResult.Ok($"Restored the database from {name}. The data from before is saved as {safety}. If your account is not in this backup you are signed out in a few minutes.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or MySqlException)
        {
            logger.LogError(ex, "The restore of {Backup} could not start.", name);
            return ServiceResult.Fail("The restore could not start: " + ex.GetBaseException().Message);
        }
        finally
        {
            settings.EndRestore();
            Gate.Release();
        }
    }

    // What the app keeps in memory is read again, and migrations newer than the backup are applied.
    private async Task TryAfterRestoreAsync()
    {
        try
        {
            await using (var db = await dbFactory.CreateDbContextAsync())
            {
                await db.Database.MigrateAsync();
            }

            await photos.LoadAsync();
            await settings.LoadAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The system could not be prepared after the restore.");
        }
    }

    /// <summary>Reads the whole file and checks that it only has the statements a backup has. Gives back the tables in it.</summary>
    internal static HashSet<string> CheckScript(string path)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StreamReader(path, Encoding.UTF8);
        foreach (var statement in SqlScript.Statements(reader))
        {
            var head = statement.TrimStart();
            if (head.StartsWith("SET ", StringComparison.OrdinalIgnoreCase)
                || head.StartsWith("DROP TABLE IF EXISTS", StringComparison.OrdinalIgnoreCase)
                || head.StartsWith("INSERT INTO", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (head.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(head, @"^CREATE TABLE\s+`((?:[^`]|``)+)`", RegexOptions.IgnoreCase);
                if (!m.Success)
                {
                    throw new InvalidDataException("The file has a table that can't be read.");
                }

                tables.Add(m.Groups[1].Value.Replace("``", "`"));
                continue;
            }

            throw new InvalidDataException("The file has something a backup does not have, so it was not run.");
        }

        if (tables.Count == 0)
        {
            throw new InvalidDataException("The file has no tables.");
        }

        return tables;
    }

    private async Task RunScriptAsync(string path, HashSet<string> tablesInBackup)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var conn = (MySqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();

        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            foreach (var statement in SqlScript.Statements(reader))
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = statement;
                cmd.CommandTimeout = 0;
                await cmd.ExecuteNonQueryAsync();
            }

            // A table that is not in the backup (the backup is older than the system) goes too; migrations bring it back.
            var existing = new List<string>();
            await using (var list = conn.CreateCommand())
            {
                list.CommandText = "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'";
                await using var r = await list.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    existing.Add(r.GetString(0));
                }
            }

            await using (var off = conn.CreateCommand())
            {
                off.CommandText = "SET FOREIGN_KEY_CHECKS = 0";
                await off.ExecuteNonQueryAsync();
            }

            foreach (var extra in existing.Where(t => !tablesInBackup.Contains(t)))
            {
                await using var drop = conn.CreateCommand();
                drop.CommandText = $"DROP TABLE IF EXISTS {Quote(extra)}";
                await drop.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await using var on = conn.CreateCommand();
            on.CommandText = "SET FOREIGN_KEY_CHECKS = 1; SET UNIQUE_CHECKS = 1";
            try
            {
                await on.ExecuteNonQueryAsync();
            }
            catch (MySqlException)
            {
                // The connection is thrown away when the context is disposed, which clears these settings anyway.
            }
        }
    }

    private async Task<bool> IsAdminAsync(int userId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive && u.RoleId == AppRoles.AdminId, ct);
    }
}

/// <summary>Cuts a text of SQL into its statements (at the semicolons that are not inside quotes), skipping the comment lines between them.</summary>
internal static class SqlScript
{
    public static IEnumerable<string> Statements(TextReader reader)
    {
        var sb = new StringBuilder();
        var inQuote = '\0';
        var atLineStart = true;
        int next;

        while ((next = reader.Read()) >= 0)
        {
            var c = (char)next;

            if (inQuote != '\0')
            {
                sb.Append(c);
                if (c == '\\' && inQuote != '`')
                {
                    // An escaped character (\' \\ \n ...) belongs to the text: copy it without looking at it.
                    var escaped = reader.Read();
                    if (escaped >= 0)
                    {
                        sb.Append((char)escaped);
                    }
                }
                else if (c == inQuote)
                {
                    inQuote = '\0';
                }

                continue;
            }

            // A comment line between statements.
            if (atLineStart && c == '-' && reader.Peek() == '-' && sb.ToString().AsSpan().Trim().IsEmpty)
            {
                reader.ReadLine();
                sb.Clear();
                atLineStart = true;
                continue;
            }

            if (c == ';')
            {
                var statement = sb.ToString().Trim();
                sb.Clear();
                if (statement.Length > 0)
                {
                    yield return statement;
                }

                atLineStart = false;
                continue;
            }

            if (c is '\'' or '"' or '`')
            {
                inQuote = c;
            }

            sb.Append(c);
            atLineStart = c == '\n';
        }

        var last = sb.ToString().Trim();
        if (last.Length > 0)
        {
            yield return last;
        }
    }
}
