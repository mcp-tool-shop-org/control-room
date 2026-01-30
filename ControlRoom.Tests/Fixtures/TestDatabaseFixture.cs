using System.IO;
using ControlRoom.Infrastructure.Storage;

namespace ControlRoom.Tests.Fixtures;

/// <summary>
/// Fixture for creating and managing test SQLite databases.
/// Uses temporary files in %TEMP% directory.
/// </summary>
public sealed class TestDatabaseFixture : IDisposable
{
    private readonly string _dbPath;
    private bool _disposed;

    public TestDatabaseFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"controlroom_test_{Guid.NewGuid()}.db");
    }

    /// <summary>
    /// Get a new Db instance for this test
    /// </summary>
    public Db CreateDb()
    {
        return new Db(_dbPath);
    }

    /// <summary>
    /// Get the database file path
    /// </summary>
    public string DbPath => _dbPath;

    /// <summary>
    /// Clear the database by deleting and recreating it
    /// </summary>
    public void Reset()
    {
        try
        {
            // Give SQLite time to release locks
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
            
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
            
            // SQLite WAL files
            var walFile = _dbPath + "-wal";
            var shmFile = _dbPath + "-shm";
            if (File.Exists(walFile)) File.Delete(walFile);
            if (File.Exists(shmFile)) File.Delete(shmFile);
        }
        catch
        {
            // Ignore cleanup errors - files will be cleaned up by temp folder cleanup
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Reset();
    }
}
