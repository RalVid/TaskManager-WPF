using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskManager.Models;
using TaskManager.Storage;

namespace TaskManager.Tests;

internal sealed class MemoryStore : ITaskStore
{
    public bool FailSave { get; set; }
    public bool FailLoad { get; set; }
    public bool FailValidation { get; set; }
    public IReadOnlyList<TaskItem> SavedTasks { get; private set; } = Array.Empty<TaskItem>();
    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<TaskItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (FailLoad)
        {
            return Task.FromException<IReadOnlyList<TaskItem>>(new IOException("Simulated load failure."));
        }

        if (FailValidation)
        {
            return Task.FromException<IReadOnlyList<TaskItem>>(
                new InvalidDataException("Simulated task validation failure."));
        }

        return Task.FromResult<IReadOnlyList<TaskItem>>(Array.Empty<TaskItem>());
    }

    public Task SaveAsync(
        IReadOnlyCollection<TaskItem> tasks,
        CancellationToken cancellationToken = default)
    {
        if (FailSave)
        {
            return Task.FromException(new IOException("Simulated save failure."));
        }

        SavedTasks = tasks.ToArray();
        SaveCount++;
        return Task.CompletedTask;
    }
}
