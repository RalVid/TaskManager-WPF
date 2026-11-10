using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using TaskManager.Storage;
using TaskManager.ViewModels;

namespace TaskManager.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        var dataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var filePath = Path.Combine(dataDirectory, "TaskManager", "tasks.json");

        _viewModel = new MainViewModel(new JsonTaskStore(filePath));
        DataContext = _viewModel;
        Loaded += WindowLoaded;
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= WindowLoaded;
        await _viewModel.InitializeAsync();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Do not close while a save is in progress.
        if (_viewModel.IsSaving)
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}
