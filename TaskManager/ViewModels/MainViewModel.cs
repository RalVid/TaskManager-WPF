using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Data;
using TaskManager.Models;
using TaskManager.MvvmHelpers;
using TaskManager.Storage;

namespace TaskManager.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ITaskStore _store;
    private readonly ObservableCollection<TaskItem> _tasks = new();

    private string _newTitle = string.Empty;
    private string _message = "Loading tasks...";
    private bool _isReady;
    private bool _isSaving;
    private TaskFilter _selectedFilter;

    public MainViewModel(ITaskStore store)
    {
        _store = store;

        // The view filters the same collection, so hidden tasks are still saved.
        var collectionViewSource = new CollectionViewSource { Source = _tasks };
        Tasks = collectionViewSource.View;
        Tasks.Filter = MatchesSelectedFilter;
        Tasks.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));

        AddTaskCommand = new RelayCommand(ExecuteAddTask, CanAddTask);
        DeleteTaskCommand = new RelayCommand(ExecuteDeleteTask, CanDeleteTask);
    }

    public ICollectionView Tasks { get; }

    public RelayCommand AddTaskCommand { get; }

    public RelayCommand DeleteTaskCommand { get; }

    public bool IsEmpty => Tasks.IsEmpty;

    public bool IsSaving => _isSaving;

    public bool CanEdit => _isReady && !IsSaving;

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public string NewTitle
    {
        get => _newTitle;
        set
        {
            if (SetProperty(ref _newTitle, value))
            {
                AddTaskCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public TaskFilter SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
            {
                Tasks.Refresh();
            }
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            var savedTasks = await _store.LoadAsync();

            foreach (var task in savedTasks)
            {
                task.PropertyChanged += TaskChanged;
                _tasks.Add(task);
            }

            _isReady = true;
            Message = string.Empty;
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            // Editing stays disabled so we never overwrite data we could not load.
            Message = "Could not load tasks. The file has not been changed. Check the file and restart the app.";
        }

        UpdateEditingState();
    }

    private bool CanAddTask()
    {
        return CanEdit && !string.IsNullOrWhiteSpace(NewTitle);
    }

    private bool CanDeleteTask(object? parameter)
    {
        return CanEdit && parameter is TaskItem task && _tasks.Contains(task);
    }

    private bool MatchesSelectedFilter(object item)
    {
        if (item is not TaskItem task)
        {
            return false;
        }

        switch (SelectedFilter)
        {
            case TaskFilter.Completed:
                return task.IsCompleted;

            case TaskFilter.Incomplete:
                return !task.IsCompleted;

            default:
                return true;
        }
    }

    private async void ExecuteAddTask()
    {
        await AddTaskAsync();
    }

    private async Task AddTaskAsync()
    {
        if (!CanAddTask())
        {
            return;
        }

        var task = new TaskItem
        {
            Title = NewTitle.Trim()
        };

        _tasks.Add(task);
        bool wasSaved = await SaveAsync();

        if (wasSaved)
        {
            task.PropertyChanged += TaskChanged;
            NewTitle = string.Empty;
        }
        else
        {
            _tasks.Remove(task);
        }
    }

    private async void ExecuteDeleteTask(object? parameter)
    {
        if (parameter is TaskItem task)
        {
            await DeleteTaskAsync(task);
        }
    }

    private async Task DeleteTaskAsync(TaskItem task)
    {
        if (!CanDeleteTask(task))
        {
            return;
        }

        // Remember the position in the full list so a failed save can restore it.
        int originalIndex = _tasks.IndexOf(task);
        task.PropertyChanged -= TaskChanged;
        _tasks.RemoveAt(originalIndex);

        bool wasSaved = await SaveAsync();

        if (!wasSaved)
        {
            task.PropertyChanged += TaskChanged;
            _tasks.Insert(originalIndex, task);
        }
    }

    private async void TaskChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(TaskItem.IsCompleted) || sender is not TaskItem task)
        {
            return;
        }

        // The filter does not rerun on item changes, so refresh it explicitly.
        Tasks.Refresh();
        bool wasSaved = await SaveAsync();

        if (!wasSaved)
        {
            // Detach while undoing so the rollback does not trigger another save.
            task.PropertyChanged -= TaskChanged;
            task.IsCompleted = !task.IsCompleted;
            task.PropertyChanged += TaskChanged;

            Tasks.Refresh();
        }
    }

    private async Task<bool> SaveAsync()
    {
        _isSaving = true;
        UpdateEditingState();
        Message = "Saving...";

        try
        {
            await _store.SaveAsync(_tasks.ToArray());

            Message = string.Empty;
            return true;
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            Message = "Could not save. The change was undone. Check file access and try again.";
            return false;
        }
        finally
        {
            _isSaving = false;
            UpdateEditingState();
        }
    }

    private void UpdateEditingState()
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(IsSaving));
        AddTaskCommand.NotifyCanExecuteChanged();
        DeleteTaskCommand.NotifyCanExecuteChanged();
    }

    private static bool IsStorageError(Exception exception)
    {
        return exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or JsonException;
    }
}
