using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TaskManager.Models;
using TaskManager.Storage;
using Xunit;

namespace TaskManager.Tests;

public sealed class JsonTaskStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "TaskManager-tests-" + Guid.NewGuid());

    private string FilePath => Path.Combine(_directory, "tasks.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task Load_ReturnsEmptyListWhenFileIsMissing()
    {
        var store = new JsonTaskStore(FilePath);

        Assert.Empty(await store.LoadAsync());
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsTasks()
    {
        var store = new JsonTaskStore(FilePath);
        var task = new TaskItem { Title = "Saved", IsCompleted = true };

        await store.SaveAsync(new[] { task });
        var loaded = (await store.LoadAsync()).Single();

        Assert.Equal(task.Id, loaded.Id);
        Assert.Equal(task.Title, loaded.Title);
        Assert.True(loaded.IsCompleted);
    }

    [Fact]
    public async Task Save_RejectsBlankTitleAndDoesNotWriteFile()
    {
        var store = new JsonTaskStore(FilePath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.SaveAsync(new[] { new TaskItem { Title = "  " } }));

        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task Save_ReplacesExistingFile()
    {
        var store = new JsonTaskStore(FilePath);
        await store.SaveAsync(new[] { new TaskItem { Title = "First" } });

        await store.SaveAsync(Array.Empty<TaskItem>());

        Assert.Empty(await store.LoadAsync());
    }

    [Fact]
    public async Task Load_ThrowsAndPreservesCorruptFile()
    {
        var store = new JsonTaskStore(FilePath);
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(FilePath, "broken json");

        await Assert.ThrowsAnyAsync<JsonException>(() => store.LoadAsync());

        Assert.Equal("broken json", await File.ReadAllTextAsync(FilePath));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[{\"Title\":\"   \"}]")]
    [InlineData("[{\"Id\":\"11111111-1111-1111-1111-111111111111\",\"Title\":\"First\"}," +
                "{\"Id\":\"11111111-1111-1111-1111-111111111111\",\"Title\":\"Second\"}]")]
    public async Task Load_RejectsAndPreservesInvalidTaskData(string document)
    {
        var store = new JsonTaskStore(FilePath);
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(FilePath, document);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());

        Assert.Equal(document, await File.ReadAllTextAsync(FilePath));
    }
}
