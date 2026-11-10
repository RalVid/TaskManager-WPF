using System;
using TaskManager.MvvmHelpers;

namespace TaskManager.Models;

public sealed class TaskItem : ObservableObject
{
    private bool _isCompleted;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title { get; init; } = string.Empty;

    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }
}
