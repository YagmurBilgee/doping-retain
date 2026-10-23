using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DopingRetain.Api.Tests;

/// <summary>
/// Runs an action right before the first INSERT command reaches the database, after the service has
/// done its own reads: simulates another request that wins the race in between.
/// </summary>
public sealed class BeforeFirstInsert(Func<Task> action) : DbCommandInterceptor
{
    public BeforeFirstInsert(Action action) : this(() =>
    {
        action();
        return Task.CompletedTask;
    })
    {
    }

    public bool Ran { get; private set; }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await RunBeforeInsertAsync(command);
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        await RunBeforeInsertAsync(command);
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private async Task RunBeforeInsertAsync(DbCommand command)
    {
        if (Ran || !command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase)) return;
        Ran = true;
        await action();
    }
}
