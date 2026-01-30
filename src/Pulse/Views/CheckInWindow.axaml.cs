using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Pulse.Models;

namespace Pulse.Views;

public partial class CheckInWindow : Window
{
    private static readonly TimeSpan AutoCloseTimeout = TimeSpan.FromMinutes(5);

    private readonly Storage _storage = new();
    private readonly DispatcherTimer _autoCloseTimer;
    private readonly DispatcherTimer _countdownTimer;
    private readonly DateTime _openedAt;
    private DateTime _autoCloseAt;
    private PulseState _state;
    private string _todayLog;
    private List<RecentTaskViewModel> _allRecentTasks = [];

    public ObservableCollection<TaskViewModel> Tasks { get; } = [];
    public string CurrentTime => DateTime.Now.ToString("HH:mm");
    public string Version => typeof(CheckInWindow).Assembly.GetName().Version?.ToString(3) ?? "?";
    public string DataDirectory => _storage.DataDirectory.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~");

    public CheckInWindow()
    {
        InitializeComponent();
        DataContext = this;

        _openedAt = DateTime.Now;
        (_state, _todayLog) = _storage.Load();

        LoadActiveTasks();
        LoadRecentTasks();

        // Auto-close after 5 minutes of inactivity
        _autoCloseAt = DateTime.Now.Add(AutoCloseTimeout);
        _autoCloseTimer = new DispatcherTimer { Interval = AutoCloseTimeout };
        _autoCloseTimer.Tick += OnAutoClose;
        _autoCloseTimer.Start();

        // Update countdown display every second
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += OnCountdownTick;
        _countdownTimer.Start();
        UpdateCountdownText();

        // Reset timer on any pointer/key activity
        PointerPressed += (_, _) => ResetAutoCloseTimer();
        KeyDown += (_, _) => ResetAutoCloseTimer();
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        UpdateCountdownText();
    }

    private void UpdateCountdownText()
    {
        var remaining = _autoCloseAt - DateTime.Now;
        if (remaining.TotalSeconds > 0)
        {
            DoneButton.Content = $"Done ({(int)remaining.TotalMinutes}:{remaining.Seconds:D2})";
        }
    }

    private void LoadActiveTasks()
    {
        foreach (var task in _state.Active)
        {
            Tasks.Add(new TaskViewModel
            {
                Description = task.Description,
                Category = task.Category,
                IsChecked = true,
                Started = task.Started,
                IsNew = false
            });
        }
    }

    private void LoadRecentTasks()
    {
        _allRecentTasks = _state.Recent
            .Select(r => new RecentTaskViewModel
            {
                Description = r.Description,
                Category = r.Category
            })
            .ToList();

        UpdateRecentTasksDisplay("");
    }

    private void UpdateRecentTasksDisplay(string filter)
    {
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? _allRecentTasks.Take(10)
            : _allRecentTasks
                .Where(r => r.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Take(10);

        RecentTasksList.ItemsSource = filtered.ToList();
    }

    private void OnAutoClose(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();
        _countdownTimer.Stop();
        var previousCheckIn = _state.LastCheckIn;

        // Stop all tasks (user was away)
        foreach (var task in Tasks.Where(t => !t.IsNew))
        {
            _todayLog = _storage.AppendToLog(_todayLog, new ActiveTask
            {
                Description = task.Description,
                Category = task.Category,
                Started = task.Started
            }, _openedAt);
        }

        _state.Active.Clear();
        _state.MissedCheckIn = _openedAt;
        _state.LastCheckIn = _openedAt;
        _storage.Save(_state, _todayLog, previousCheckIn);

        // Show "I'm back" overlay instead of closing
        ImBackPanel.IsVisible = true;
        DoneButton.Content = "Done";
    }

    private void OnImBackClick(object? sender, RoutedEventArgs e)
    {
        ImBackPanel.IsVisible = false;

        // Reload tasks from recent (all unchecked)
        Tasks.Clear();
        var recentTasks = _state.Recent.Take(5);
        foreach (var recent in recentTasks)
        {
            Tasks.Add(new TaskViewModel
            {
                Description = recent.Description,
                Category = recent.Category,
                IsChecked = false,
                Started = DateTime.Now,
                IsNew = true
            });
        }

        // Reset auto-close timer
        ResetAutoCloseTimer();
        _countdownTimer.Start();
    }

    private void OnDoneClick(object? sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        _countdownTimer.Stop();
        var now = DateTime.Now;
        var previousCheckIn = _state.LastCheckIn;

        // Process unchecked tasks (stopped)
        foreach (var task in Tasks.Where(t => !t.IsChecked && !t.IsNew))
        {
            _todayLog = _storage.AppendToLog(_todayLog, new ActiveTask
            {
                Description = task.Description,
                Category = task.Category,
                Started = task.Started
            }, now);

            _state.Active.RemoveAll(a =>
                a.Description.Equals(task.Description, StringComparison.OrdinalIgnoreCase));
        }

        // Add new checked tasks to active
        foreach (var task in Tasks.Where(t => t.IsChecked && t.IsNew))
        {
            _state.Active.Add(new ActiveTask
            {
                Description = task.Description,
                Category = task.Category,
                Started = now
            });

            _storage.AddToRecent(_state, new RecentTask
            {
                Description = task.Description,
                Category = task.Category
            });
        }

        _state.LastCheckIn = now;
        _state.MissedCheckIn = null;
        _storage.Save(_state, _todayLog, previousCheckIn);

        Close();
    }

    private void OnShowAddTaskClick(object? sender, RoutedEventArgs e)
    {
        // Pause auto-close while working in modal
        _autoCloseTimer.Stop();
        _countdownTimer.Stop();
        DoneButton.Content = "Done";

        AddTaskPanel.IsVisible = true;
        AddTaskButton.IsVisible = false;
        TaskSearchBox.Text = "";
        TaskSearchBox.Focus();
    }

    private void OnCancelAddClick(object? sender, RoutedEventArgs e)
    {
        AddTaskPanel.IsVisible = false;
        AddTaskButton.IsVisible = true;

        // Resume auto-close timer
        ResetAutoCloseTimer();
        _countdownTimer.Start();
    }

    private void OnTaskSearchChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateRecentTasksDisplay(TaskSearchBox.Text ?? "");
    }

    private void OnTaskSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnAddTaskClick(sender, e);
        }
    }

    private void OnRecentTaskSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (RecentTasksList.SelectedItem is RecentTaskViewModel recent)
        {
            TaskSearchBox.Text = recent.Description;
            SetCategoryRadio(recent.Category);
            RecentTasksList.SelectedItem = null;
        }
    }

    private void OnAddTaskClick(object? sender, RoutedEventArgs e)
    {
        var description = TaskSearchBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(description))
            return;

        // Check if already in list
        if (Tasks.Any(t => t.Description.Equals(description, StringComparison.OrdinalIgnoreCase)))
            return;

        var category = GetSelectedCategory();

        Tasks.Add(new TaskViewModel
        {
            Description = description,
            Category = category,
            IsChecked = true,
            Started = DateTime.Now,
            IsNew = true
        });

        AddTaskPanel.IsVisible = false;
        AddTaskButton.IsVisible = true;

        // Resume auto-close timer
        ResetAutoCloseTimer();
        _countdownTimer.Start();
    }

    private Category GetSelectedCategory()
    {
        if (CategoryWork.IsChecked == true) return Category.Work;
        if (CategoryHobby.IsChecked == true) return Category.Hobby;
        if (CategoryRelationship.IsChecked == true) return Category.Relationship;
        return Category.Other;
    }

    private void SetCategoryRadio(Category category)
    {
        CategoryWork.IsChecked = category == Category.Work;
        CategoryHobby.IsChecked = category == Category.Hobby;
        CategoryRelationship.IsChecked = category == Category.Relationship;
        CategoryOther.IsChecked = category == Category.Other;
    }

    private void OnCategoryClick(object? sender, RoutedEventArgs e)
    {
        // Ensure only one category is selected (radio-like behavior)
        if (sender is ToggleButton clicked)
        {
            CategoryWork.IsChecked = clicked == CategoryWork;
            CategoryHobby.IsChecked = clicked == CategoryHobby;
            CategoryRelationship.IsChecked = clicked == CategoryRelationship;
            CategoryOther.IsChecked = clicked == CategoryOther;
        }
    }

    private void ResetAutoCloseTimer()
    {
        _autoCloseTimer.Stop();
        _autoCloseAt = DateTime.Now.Add(AutoCloseTimeout);
        UpdateCountdownText();
        _autoCloseTimer.Start();
    }

    private void OnTaskCardPressed(object? sender, PointerPressedEventArgs e)
    {
        ResetAutoCloseTimer();
        if (sender is Border { DataContext: TaskViewModel task })
        {
            task.IsChecked = !task.IsChecked;
        }
    }

    private async void OnDataDirectoryClick(object? sender, PointerPressedEventArgs e)
    {
        ResetAutoCloseTimer();

        var storage = StorageProvider;
        var startFolder = await storage.TryGetFolderFromPathAsync(new Uri("file://" + _storage.DataDirectory));

        var result = await storage.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select Data Directory",
            SuggestedStartLocation = startFolder,
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            var path = result[0].Path.LocalPath;
            Storage.SaveDataDirectory(path);
            // Update the display
            DataDirectoryText.Text = path.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~");
        }
    }
}

public class TaskViewModel : INotifyPropertyChanged
{
    private bool _isChecked;

    public string Description { get; set; } = "";
    public Category Category { get; set; }
    public DateTime Started { get; set; }
    public bool IsNew { get; set; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            OnPropertyChanged();
        }
    }

    public string CategoryName => Category.ToString();
    public bool IsWork => Category == Category.Work;
    public bool IsHobby => Category == Category.Hobby;
    public bool IsRelationship => Category == Category.Relationship;
    public bool IsOther => Category == Category.Other;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public class RecentTaskViewModel
{
    public string Description { get; set; } = "";
    public Category Category { get; set; }
    public string CategoryName => Category.ToString();
    public bool IsWork => Category == Category.Work;
    public bool IsHobby => Category == Category.Hobby;
    public bool IsRelationship => Category == Category.Relationship;
    public bool IsOther => Category == Category.Other;
}
