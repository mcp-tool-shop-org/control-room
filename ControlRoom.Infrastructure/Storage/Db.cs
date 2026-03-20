using Microsoft.Data.Sqlite;

namespace ControlRoom.Infrastructure.Storage;

public sealed class Db
{
    private readonly string _connectionString;

    public Db(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath)!;
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new InvalidOperationException(
                $"Cannot create database directory '{dir}': {ex.Message}", ex);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        try
        {
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA foreign_keys = ON;
                """;
            cmd.ExecuteNonQuery();

            return conn;
        }
        catch (SqliteException ex)
        {
            conn.Dispose();
            throw new InvalidOperationException(
                $"Failed to open database: {ex.Message}", ex);
        }
    }
}
