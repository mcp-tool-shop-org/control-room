using Microsoft.Data.Sqlite;

namespace ControlRoom.Infrastructure.Storage;

public sealed class Migrator
{
    private readonly Db _db;

    public Migrator(Db db) => _db = db;

    public void EnsureCreated(string schemaSql)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = schemaSql;
            cmd.ExecuteNonQuery();
            tx.Commit();
        }
        catch (SqliteException ex)
        {
            try { tx.Rollback(); } catch { /* best effort */ }
            throw new InvalidOperationException(
                $"Database migration failed: {ex.Message}", ex);
        }
    }
}
