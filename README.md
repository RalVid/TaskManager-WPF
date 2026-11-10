# Task Manager

A small WPF application built with MVVM (.NET 8) for creating tasks, marking them as done, deleting them and filtering by All, Done or Not done. Tasks are saved automatically as JSON.

## Run

Requires Windows and the .NET 8 SDK (or later).

```
dotnet run --project TaskManager
dotnet test TaskManager.Tests
```

## Structure

- `Models`: `TaskItem` (id, title, completion state).
- `ViewModels`: `MainViewModel` with input, validation, commands and filtering.
- `Views`: `MainWindow` with XAML bindings and no business logic.
- `MvvmHelpers`: `ObservableObject` (INotifyPropertyChanged) and `RelayCommand` (ICommand).
- `Storage`: `ITaskStore` and `JsonTaskStore`, so persistence can be replaced or faked in tests.
- `TaskManager.Tests`: xUnit tests for the ViewModel (with a fake store) and the JSON store.

## Notes

- MVVM: the view binds to `MainViewModel` through `INotifyPropertyChanged` and `ICommand`. The task list is an `ObservableCollection`.
- Tasks are kept in an `ObservableCollection`. Filtering uses an `ICollectionView` over it, so hidden tasks are still saved.
- Titles are trimmed and cannot be empty. Duplicate titles are allowed.
- Data is stored in `%LOCALAPPDATA%\TaskManager\tasks.json`. Saving writes a temporary file and then replaces the original.
- If a save fails, the change is undone and a message is shown. If loading fails, editing is disabled so the file is not overwritten.
- Only one running instance is supported.
