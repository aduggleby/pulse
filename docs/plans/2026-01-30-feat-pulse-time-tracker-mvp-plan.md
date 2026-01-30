---
title: "feat: Pulse Time Tracker MVP"
type: feat
date: 2026-01-30
deepened: 2026-01-30
reviewed: 2026-01-30
---

# Pulse Time Tracker MVP

## Overview

Build Pulse, a minimal hourly time tracker. Pop up every hour, check off tasks, save to markdown. Single file for state + today's log, archived to dated files on day change.

## Core Behavior

```
While app is running:
    Wait until next hour boundary
    Show popup with active tasks (pre-checked)
    User checks/unchecks tasks, adds new ones
    User clicks "Done"
    Save state + append to today's log
    Hide popup

On day change:
    Archive today's log to ~/Me/Info/pulse/YYYY-MM-DD.md
    Reset Pulse.md for new day (keep frontmatter state)
```

## Data Model

### File Structure

```
~/Me/Info/
├── Pulse.md                    # Current state + today's log
└── pulse/
    ├── 2026-01-29.md          # Yesterday's archived log
    ├── 2026-01-28.md          # Older logs...
    └── ...
```

### Pulse.md Format

```markdown
---
active:
  - description: Code review
    category: Work
    started: 2026-01-30T14:00
  - description: Listening to music
    category: Hobby
    started: 2026-01-30T09:00
recent:
  - description: Writing Pulse spec
    category: Work
  - description: Code review
    category: Work
  - description: Reading
    category: Hobby
  # ... up to 100 items (LRU)
lastCheckIn: 2026-01-30T15:00
missedCheckIn: null
---

# Pulse Log

## 2026-01-30

- [Work] Writing Pulse spec (09:00 - 14:00)
- [Hobby] Listening to music (09:00 - ongoing)
- [Work] Code review (14:00 - ongoing)
```

### Archived File Format (pulse/2026-01-29.md)

```markdown
# 2026-01-29

- [Work] Client project (10:00 - 18:00)
- [Hobby] Reading (20:00 - 23:00)
```

No frontmatter in archived files - just the log entries.

## Technical Approach

### Architecture

```
/src/Pulse/
├── Pulse.csproj
├── Program.cs                  # Entry point
├── App.axaml                   # App resources
├── App.axaml.cs                # Timer, lifecycle
│
├── Models/
│   ├── PulseState.cs           # Frontmatter model
│   └── Category.cs             # Enum
│
├── Views/
│   ├── CheckInWindow.axaml     # Main popup
│   └── CheckInWindow.axaml.cs  # Code-behind
│
├── Storage.cs                  # Read/write Pulse.md, archive
└── Assets/
    └── icon.png
```

**~8 files, ~400 LOC estimated**

### Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 10 |
| UI | Avalonia UI 11.x |
| YAML | YamlDotNet 16.x |

### Models

```csharp
// Models/PulseState.cs
public class PulseState
{
    public List<ActiveTask> Active { get; set; } = [];
    public List<RecentTask> Recent { get; set; } = [];  // Max 100, LRU
    public DateTime? LastCheckIn { get; set; }
    public DateTime? MissedCheckIn { get; set; }
}

public class ActiveTask
{
    public string Description { get; set; } = "";
    public Category Category { get; set; }
    public DateTime Started { get; set; }
}

public class RecentTask
{
    public string Description { get; set; } = "";
    public Category Category { get; set; }
}

// Models/Category.cs
public enum Category { Work, Hobby, Relationship, Other }
```

### Storage (~100 lines)

```csharp
public class Storage
{
    private const string PulsePath = "~/Me/Info/Pulse.md";
    private const string ArchiveDir = "~/Me/Info/pulse";
    private const int MaxRecentTasks = 100;

    public PulseState LoadState() { /* Parse YAML frontmatter */ }
    public string LoadTodayLog() { /* Get markdown body */ }

    public void Save(PulseState state, string todayLog)
    {
        // Check if day changed - archive if needed
        if (state.LastCheckIn?.Date < DateTime.Today)
        {
            ArchiveDay(state.LastCheckIn.Value.Date);
        }

        // Write frontmatter + today's log
        var content = SerializeFrontmatter(state) + "\n" + todayLog;
        WriteAtomic(PulsePath, content);
    }

    private void ArchiveDay(DateTime date)
    {
        // Move today's log section to pulse/YYYY-MM-DD.md
        var todayLog = LoadTodayLog();
        if (!string.IsNullOrEmpty(todayLog))
        {
            var archivePath = Path.Combine(ArchiveDir, $"{date:yyyy-MM-dd}.md");
            File.WriteAllText(archivePath, $"# {date:yyyy-MM-dd}\n\n{todayLog}");
        }
    }

    public void AddToRecent(RecentTask task)
    {
        // LRU: move to front if exists, add if new, trim to 100
    }
}
```

### Timer (in App.axaml.cs)

```csharp
public partial class App : Application
{
    private DispatcherTimer _timer;

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();

        // Calculate delay to next hour boundary
        var now = DateTime.Now;
        var nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);

        _timer = new DispatcherTimer { Interval = nextHour - now };
        _timer.Tick += OnHourlyTick;
        _timer.Start();
    }

    private void OnHourlyTick(object? sender, EventArgs e)
    {
        _timer.Interval = TimeSpan.FromHours(1);  // Reset to hourly
        ShowCheckInWindow();
    }

    private void ShowCheckInWindow()
    {
        var window = new CheckInWindow();
        window.Show();
        window.Activate();
    }
}
```

### Check-in Window

**UI:**
```
┌─────────────────────────────────────┐
│  What are you doing?          15:00 │
├─────────────────────────────────────┤
│  ☑ Code review               [Work] │
│  ☑ Listening to music       [Hobby] │
├─────────────────────────────────────┤
│  [+ Add Task]                       │
├─────────────────────────────────────┤
│              [Done]                 │
└─────────────────────────────────────┘
```

**Add Task (inline expandable panel):**
```
┌─────────────────────────────────────┐
│  [Search or type new task...    ] 🔍│
│  ┌─ Recent ─────────────────────┐   │
│  │ Writing Pulse spec    [Work] │   │
│  │ Reading              [Hobby] │   │
│  │ Email                 [Work] │   │
│  └──────────────────────────────┘   │
│  Category:                          │
│  (•) Work  ( ) Hobby                │
│  ( ) Relationship  ( ) Other        │
│           [Cancel]  [Add]           │
└─────────────────────────────────────┘
```

**Auto-close behavior:**
- 5-minute timer starts when popup opens
- Any interaction resets the timer
- On timeout: stop all active tasks, set `missedCheckIn`, close popup

```csharp
public partial class CheckInWindow : Window
{
    private readonly Storage _storage;
    private readonly DispatcherTimer _autoCloseTimer;
    private readonly DateTime _openedAt;
    private PulseState _state;
    private ObservableCollection<TaskViewModel> _tasks = new();

    public CheckInWindow()
    {
        InitializeComponent();
        _storage = new Storage();
        _state = _storage.LoadState();
        _openedAt = DateTime.Now;

        LoadActiveTasks();

        // Auto-close after 5 minutes of inactivity
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        _autoCloseTimer.Tick += OnAutoClose;
        _autoCloseTimer.Start();
    }

    private void LoadActiveTasks()
    {
        foreach (var task in _state.Active)
        {
            _tasks.Add(new TaskViewModel
            {
                Description = task.Description,
                Category = task.Category,
                IsChecked = true,
                Started = task.Started
            });
        }
    }

    private void OnAutoClose(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();

        // Stop all tasks at the time popup opened
        foreach (var task in _tasks.Where(t => t.IsChecked))
        {
            AppendToLog(task, _openedAt);
        }

        _state.Active.Clear();
        _state.MissedCheckIn = _openedAt;
        _state.LastCheckIn = _openedAt;
        _storage.Save(_state, GetTodayLog());

        Close();
    }

    private void OnDoneClick(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        var now = DateTime.Now;

        // Process unchecked tasks (stopped)
        foreach (var task in _tasks.Where(t => !t.IsChecked))
        {
            AppendToLog(task, now);
            _state.Active.RemoveAll(a => a.Description == task.Description);
        }

        // Add new checked tasks to active
        foreach (var task in _tasks.Where(t => t.IsChecked && t.IsNew))
        {
            _state.Active.Add(new ActiveTask
            {
                Description = task.Description,
                Category = task.Category,
                Started = now
            });
            _storage.AddToRecent(new RecentTask
            {
                Description = task.Description,
                Category = task.Category
            });
        }

        _state.LastCheckIn = now;
        _state.MissedCheckIn = null;
        _storage.Save(_state, GetTodayLog());

        Close();
    }

    private void ResetAutoCloseTimer()
    {
        _autoCloseTimer.Stop();
        _autoCloseTimer.Start();
    }

    // Called on any user interaction
    private void OnUserInteraction(object sender, EventArgs e)
    {
        ResetAutoCloseTimer();
    }
}
```

---

## Implementation: One Phase

Build it all, ship it, use it for 2 weeks.

**Tasks:**
- [ ] Create Avalonia project with .NET 10
- [ ] Implement `Storage.cs` - read/write Pulse.md with YAML frontmatter
- [ ] Implement day archiving - move log to `pulse/YYYY-MM-DD.md`
- [ ] Add hourly timer in App.axaml.cs
- [ ] Create `CheckInWindow` - task checkboxes, add task panel
- [ ] Implement 100 recent tasks with search/filter
- [ ] Add 5-minute auto-close timer
- [ ] Add category radio buttons
- [ ] Test full flow: hourly popup → check tasks → save → archive on day change

**Estimated:** ~400 LOC, weekend project

---

## Features Included

| Feature | Status |
|---------|--------|
| Hourly popup at hour boundary | ✅ |
| Task checkboxes (check=continue, uncheck=stop) | ✅ |
| Categories (Work, Hobby, Relationship, Other) | ✅ |
| Add new task with recent search (100 items) | ✅ |
| Auto-close after 5 minutes | ✅ |
| Single Pulse.md with YAML frontmatter | ✅ |
| Day archive to dated files | ✅ |
| LRU recent tasks list | ✅ |

## Features Cut (Add Later If Needed)

| Feature | Add When |
|---------|----------|
| System tray icon | After 2 weeks of use |
| Recovery flow for missed check-ins | If it becomes a problem |
| Sleep/wake handling | If timer breaks after sleep |
| Keyboard shortcuts | When muscle memory demands it |

---

## Success Criteria

- [ ] Popup appears at the top of every hour
- [ ] Active tasks shown pre-checked
- [ ] Unchecking stops a task (records end time)
- [ ] Can add tasks with category from recent list
- [ ] Auto-closes after 5 min inactivity, marks tasks stopped
- [ ] State persists in `~/Me/Info/Pulse.md`
- [ ] Day change archives to `~/Me/Info/pulse/YYYY-MM-DD.md`
- [ ] Recent tasks list stays at max 100 (LRU)

---

## File Summary

| File | ~Lines | Purpose |
|------|--------|---------|
| `Pulse.csproj` | 20 | Project, dependencies |
| `Program.cs` | 5 | Entry point |
| `App.axaml` | 15 | Resources |
| `App.axaml.cs` | 40 | Timer, lifecycle |
| `Models/PulseState.cs` | 30 | Data models |
| `Models/Category.cs` | 5 | Enum |
| `Views/CheckInWindow.axaml` | 80 | UI layout |
| `Views/CheckInWindow.axaml.cs` | 150 | Logic, auto-close |
| `Storage.cs` | 100 | File I/O, archive |

**Total: ~445 lines**
