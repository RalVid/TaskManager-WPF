using System.ComponentModel;
using System.Linq;
using TaskManager.Models;
using TaskManager.ViewModels;
using Xunit;

namespace TaskManager.Tests;

public class MainViewModelTests
{
    // MemoryStore completes synchronously, so commands finish before the next statement.
    private static MainViewModel CreateLoaded(MemoryStore store)
    {
        var viewModel = new MainViewModel(store);
        viewModel.InitializeAsync().GetAwaiter().GetResult();
        return viewModel;
    }

    private static void Add(MainViewModel viewModel, string title)
    {
        viewModel.NewTitle = title;
        viewModel.AddTaskCommand.Execute(null);
    }

    private static TaskItem[] Visible(MainViewModel viewModel) =>
        viewModel.Tasks.Cast<TaskItem>().ToArray();

    [Fact]
    public void Add_RejectsWhitespaceTitle()
    {
        var viewModel = CreateLoaded(new MemoryStore());

        viewModel.NewTitle = "   ";

        Assert.False(viewModel.AddTaskCommand.CanExecute(null));
    }

    [Fact]
    public void Add_TrimsTitleAndClearsInput()
    {
        var viewModel = CreateLoaded(new MemoryStore());

        Add(viewModel, "  Test  ");

        var task = Assert.Single(Visible(viewModel));
        Assert.Equal("Test", task.Title);
        Assert.False(task.IsCompleted);
        Assert.Equal(string.Empty, viewModel.NewTitle);
    }

    [Fact]
    public void Filter_UpdatesWhenCompletionChanges()
    {
        var viewModel = CreateLoaded(new MemoryStore());
        Add(viewModel, "Test");
        var task = Visible(viewModel).Single();

        viewModel.SelectedFilter = TaskFilter.Completed;
        Assert.True(viewModel.IsEmpty);

        task.IsCompleted = true;
        Assert.False(viewModel.IsEmpty);

        viewModel.SelectedFilter = TaskFilter.Incomplete;
        Assert.True(viewModel.IsEmpty);

        task.IsCompleted = false;
        Assert.False(viewModel.IsEmpty);

        viewModel.SelectedFilter = TaskFilter.All;
        Assert.Single(Visible(viewModel));
    }

    [Fact]
    public void FailedSave_RollsBackCompletion()
    {
        var store = new MemoryStore();
        var viewModel = CreateLoaded(store);
        Add(viewModel, "Saved task");
        var task = Visible(viewModel).Single();

        store.FailSave = true;
        task.IsCompleted = true;

        Assert.False(task.IsCompleted);
    }

    [Fact]
    public void FailedSave_RollsBackAddAndKeepsInput()
    {
        var store = new MemoryStore();
        var viewModel = CreateLoaded(store);
        Add(viewModel, "Saved task");

        store.FailSave = true;
        Add(viewModel, "Unsaved");

        Assert.Single(Visible(viewModel));
        Assert.Equal("Unsaved", viewModel.NewTitle);
    }

    [Fact]
    public void FailedLoad_BlocksEditing()
    {
        var viewModel = CreateLoaded(new MemoryStore { FailLoad = true });

        Assert.False(viewModel.CanEdit);
    }

    [Fact]
    public void InvalidData_BlocksEditing()
    {
        var viewModel = CreateLoaded(new MemoryStore { FailValidation = true });

        Assert.False(viewModel.CanEdit);
    }

    [Fact]
    public void Delete_RejectsMissingOrUnrelatedTask()
    {
        var viewModel = CreateLoaded(new MemoryStore());
        Add(viewModel, "Task");

        Assert.False(viewModel.DeleteTaskCommand.CanExecute(null));
        Assert.False(viewModel.DeleteTaskCommand.CanExecute(new TaskItem { Title = "Not in the list" }));
    }

    [Fact]
    public void Delete_RemovesRequestedRowWithDuplicateTitlesUnderFilterAndSort()
    {
        var store = new MemoryStore();
        var viewModel = CreateLoaded(store);
        foreach (var title in new[] { "Hidden task", "Duplicate", "Duplicate" })
        {
            Add(viewModel, title);
        }

        var tasks = Visible(viewModel);
        tasks[0].IsCompleted = true;
        viewModel.SelectedFilter = TaskFilter.Incomplete;
        viewModel.Tasks.SortDescriptions.Add(
            new SortDescription(nameof(TaskItem.Title), ListSortDirection.Descending));

        viewModel.DeleteTaskCommand.Execute(tasks[1]);

        Assert.Same(tasks[2], Visible(viewModel).Single());
        Assert.Equal(new[] { tasks[0].Id, tasks[2].Id }, store.SavedTasks.Select(t => t.Id));
        Assert.False(viewModel.DeleteTaskCommand.CanExecute(tasks[1]));

        int saveCount = store.SaveCount;
        tasks[1].IsCompleted = true;
        Assert.Equal(saveCount, store.SaveCount);

        viewModel.DeleteTaskCommand.Execute(tasks[2]);
        Assert.True(viewModel.IsEmpty);
        Assert.Same(tasks[0], store.SavedTasks.Single());
    }

    [Fact]
    public void Delete_IsDisabledBeforeLoading()
    {
        var viewModel = new MainViewModel(new MemoryStore());

        Assert.False(viewModel.DeleteTaskCommand.CanExecute(new TaskItem()));
    }

    [Fact]
    public void FailedDelete_RestoresOrderAndCanBeRetried()
    {
        var store = new MemoryStore();
        var viewModel = CreateLoaded(store);
        foreach (var title in new[] { "First", "Middle", "Last" })
        {
            Add(viewModel, title);
        }

        var originalTasks = Visible(viewModel);
        store.FailSave = true;
        viewModel.DeleteTaskCommand.Execute(originalTasks[1]);

        Assert.Equal(originalTasks, Visible(viewModel));
        Assert.Equal(originalTasks, store.SavedTasks);
        Assert.True(viewModel.CanEdit);

        store.FailSave = false;
        int saveCount = store.SaveCount;
        originalTasks[1].IsCompleted = true;
        Assert.Equal(saveCount + 1, store.SaveCount);

        viewModel.DeleteTaskCommand.Execute(originalTasks[1]);
        Assert.Equal(2, store.SavedTasks.Count);
    }
}
