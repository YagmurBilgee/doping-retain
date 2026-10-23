using DopingRetain.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DopingRetain.Api.Tests;

/// <summary>A uniquely named SQLite file in the temp folder; only this file is ever deleted.</summary>
public sealed class TemporaryDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"doping-retain-test-{Guid.NewGuid():N}.db");

    public AppDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_path}")
            .AddInterceptors(interceptors)
            .Options);

    public void Dispose()
    {
        // Pooled connections keep the file open; release them before removing this database's files.
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-journal", "-wal", "-shm" })
        {
            File.Delete(_path + suffix);
        }
    }
}
