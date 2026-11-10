using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TaskManager.Models;

namespace TaskManager.Storage;

public sealed class JsonTaskStore : ITaskStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public JsonTaskStore(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<IReadOnlyList<TaskItem>> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = File.OpenRead(_filePath);
            var tasks = await JsonSerializer.DeserializeAsync<List<TaskItem>>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (tasks is null)
            {
                throw new InvalidDataException("The task file does not contain a list.");
            }

            ValidateTasks(tasks);
            return tasks;
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<TaskItem>();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<TaskItem>();
        }

        // Other errors propagate on purpose: treating a corrupt file as empty
        // would let the next save overwrite the user's data.
    }

    public async Task SaveAsync(
        IReadOnlyCollection<TaskItem> tasks,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporaryPath = _filePath + ".tmp";

        try
        {
            // Write to a temp file first so a failed save cannot leave partial JSON.
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    tasks,
                    SerializerOptions,
                    cancellationToken);
            }

            if (File.Exists(_filePath))
            {
                File.Replace(temporaryPath, _filePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, _filePath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ValidateTasks(IReadOnlyCollection<TaskItem> tasks)
    {
        bool hasInvalidTask = tasks.Any(task =>
            task is null ||
            task.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(task.Title));

        if (hasInvalidTask)
        {
            throw new InvalidDataException("The task file contains invalid tasks.");
        }

        bool hasDuplicateIds = tasks.Select(task => task.Id).Distinct().Count() != tasks.Count;

        if (hasDuplicateIds)
        {
            throw new InvalidDataException("The task file contains duplicate task IDs.");
        }
    }
}
