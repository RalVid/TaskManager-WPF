using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskManager.Models;

namespace TaskManager.Storage;

public interface ITaskStore
{
    // A missing file means no tasks yet. Unreadable or invalid data throws.
    Task<IReadOnlyList<TaskItem>> LoadAsync(CancellationToken cancellationToken = default);

    // Saves all tasks, including those hidden by the current filter.
    Task SaveAsync(IReadOnlyCollection<TaskItem> tasks, CancellationToken cancellationToken = default);
}
