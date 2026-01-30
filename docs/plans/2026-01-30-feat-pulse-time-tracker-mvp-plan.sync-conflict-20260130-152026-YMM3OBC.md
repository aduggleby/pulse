---
title: "feat: Pulse Time Tracker MVP"
type: feat
date: 2026-01-30
deepened: 2026-01-30
---

# Pulse Time Tracker MVP

## Enhancement Summary

**Deepened on:** 2026-01-30
**Research agents used:** architecture-strategist, performance-oracle, code-simplicity-reviewer, security-sentinel, pattern-recognition-specialist, best-practices-researcher, framework-docs-researcher, Context7

### Key Improvements
1. **Simplified architecture** - Reduced from 7 phases to 3, from 4 ViewModels to 1
2. **Thread-safe patterns** - Added proper UI thread dispatch and power event handling
3. **Security hardening** - YAML safe deserialization, path validation, atomic writes
4. **Performance optimizations** - ReadyToRun compilation, pooled buffers, timer alignment

### Critical Insights Discovered
- System sleep/wake requires explicit handling via `SystemEvents.PowerModeChanged`
- YamlDotNet 16.x has thread safety fixes - reuse serializer instances
- Avalonia TrayIcon works on Linux via XWayland (confirmed on Ubuntu)
- `PeriodicTimer` self-corrects drift but doesn't account for system sleep

---

## Overview

Build Pulse, a minimal hourly time tracker desktop application for Linux. The app pops up every hour asking "What are you doing?", lets users check off active tasks, add new ones, and logs everything to a single markdown file with YAML frontmatter.

## Problem Statement / Motivation

Time tracking apps are either too complex (Toggl, Clockify) or require constant manual input. Pulse takes a different approach: **interrupt-driven awareness**. By asking once per hour what you're doing, it builds honest time logs without the friction of starting/stopping timers.

The markdown-based storage makes data:
- Human-readable and editable
- Git-friendly for version control
- Obsidian-compatible (frontmatter shows as document properties)

## Proposed Solution

A .NET 10 desktop application using Avalonia UI that:
1. Runs as a background service with system tray icon
2. Shows a popup window every hour (configurable)
3. Persists all state to a single markdown file (`~/Me/Info/Pulse.md`)
4. Handles missed check-ins gracefully with recovery flow

## Technical Approach

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Pulse Application                        │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐   │
│  │   Views      │  │  ViewModels  │  │    Services      │   │
│  │              │  │              │  │                  │   │
│  │ MainWindow   │◄─┤ CheckIn      │  │ HourlyTimer      │   │
│  │ CheckInPopup │  │   ViewModel  │  │  (BackgroundSvc) │   │
│  │              │  │              │  │                  │   │
│  └──────────────┘  └──────────────┘  │ DataService      │   │
│                           ▲          │ (Markdown R/W)   │   │
│                           │          │                  │   │
│                           └──────────┤                  │   │
│                                      └──────────────────┘   │
│                                              │              │
│                                              ▼              │
│                                    ┌──────────────────┐     │
│                                    │   Pulse.md       │     │
│                                    │  (YAML + MD)     │     │
│                                    └──────────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

### Research Insights: Architecture

**Recommended Simplifications:**
- Merge AddTaskDialog into CheckInPopup as an inline panel
- Merge RecoveryDialog into CheckInPopup as conditional header
- Remove `IDataService` interface - only one implementation exists
- Remove `PopupCoordinator` - just dispatch to UI thread directly

**Thread Marshaling Pattern (Critical):**
```csharp
// BackgroundService runs on thread pool - must dispatch to UI
await Dispatcher.UIThread.InvokeAsync(async () =>
{
    var popup = new CheckInPopup { DataContext = new CheckInViewModel() };
    await popup.ShowDialog(GetMainWindow());
});
```

**Service Lifetime Guidelines:**
| Service | Lifetime | Reason |
|---------|----------|--------|
| DataService | Singleton | File lock management, caching |
| HourlyTimer | Singleton | Single timer instance |
| ViewModels | Transient | Fresh state per popup |

---

### Technology Stack

| Component | Technology | Version |
|-----------|------------|---------|
| Runtime | .NET 10 | 10.0 |
| UI Framework | Avalonia UI | 11.3.x |
| MVVM | CommunityToolkit.Mvvm | 8.4.x |
| YAML | YamlDotNet | 16.3.x |
| Markdown | Markdig | 0.38.x |
| Hosting | Microsoft.Extensions.Hosting | 10.0.x |

### Research Insights: Performance

**Recommended .csproj settings:**
```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PublishReadyToRun>true</PublishReadyToRun>
    <TieredCompilation>true</TieredCompilation>
    <TieredPGO>true</TieredPGO>
    <ServerGarbageCollection>false</ServerGarbageCollection>
</PropertyGroup>
```

**Performance Targets:**
| Metric | Target |
|--------|--------|
| Cold startup | < 500ms |
| File read + parse | < 2ms |
| File serialize + write | < 5ms |
| Memory idle | < 50MB |
| Timer drift | < 50ms/day |

---

### Project Structure (Simplified)

```
/src/Pulse/
├── Pulse.csproj
├── Program.cs                    # Entry + host configuration
├── App.axaml                     # Application, themes, tray icon
├── App.axaml.cs
│
├── Models/
│   ├── PulseData.cs              # Root model (active, recent, timestamps)
│   └── Category.cs               # Enum: Work, Hobby, Relationship, Other
│
├── ViewModels/
│   └── CheckInViewModel.cs       # THE viewmodel (check-in, add task, recovery)
│
├── Views/
│   ├── MainWindow.axaml          # Tray host (minimal, hidden)
│   └── CheckInPopup.axaml        # THE popup (inline add task, conditional recovery)
│
├── Services/
│   ├── DataService.cs            # Read/write Pulse.md (no interface needed)
│   └── HourlyTimer.cs            # BackgroundService with PeriodicTimer
│
└── Assets/
    └── icon.png                  # Single icon for app and tray
```

### Research Insights: Simplification

**Removed from original plan:**
| Item | Reason | LOC Saved |
|------|--------|-----------|
| `IDataService.cs` | YAGNI - only one implementation | ~15 |
| `IPopupService.cs` | YAGNI - unnecessary abstraction | ~10 |
| `PopupService.cs` | Just use Dispatcher directly | ~50 |
| `SettingsService.cs` | Inline into Program.cs | ~40 |
| `AddTaskDialog.axaml` + VM | Inline into CheckInPopup | ~150 |
| `RecoveryDialog.axaml` + VM | Conditional in CheckInPopup | ~120 |
| `ViewLocator.cs` | Hardcode view resolution | ~25 |
| `ViewModelBase.cs` | Use ObservableObject directly | ~15 |

**Total estimated reduction: ~40-50% of planned codebase**

---

### Data Model

#### Frontmatter Schema (YAML)

```yaml
active:                           # Currently running tasks
  - description: "Code review"
    category: Work
    started: 2026-01-30T14:00     # ISO 8601 timestamp

recent:                           # Last 100 unique tasks (LRU)
  - description: "Writing Pulse spec"
    category: Work

lastCheckIn: 2026-01-30T15:00     # Last successful check-in
missedCheckIn: null               # Timestamp if auto-close happened
```

#### C# Models

```csharp
// Models/PulseData.cs
public class PulseData
{
    public List<ActiveTask> Active { get; set; } = [];
    public List<RecentTask> Recent { get; set; } = [];
    public DateTime? LastCheckIn { get; set; }
    public DateTime? MissedCheckIn { get; set; }
}

public class ActiveTask
{
    public string Description { get; set; } = string.Empty;
    public Category Category { get; set; }
    public DateTime Started { get; set; }
}

public class RecentTask
{
    public string Description { get; set; } = string.Empty;
    public Category Category { get; set; }
}

// Models/Category.cs
public enum Category { Work, Hobby, Relationship, Other }
```

### Research Insights: YAML Parsing

**Safe YamlDotNet Configuration:**
```csharp
// Build once, reuse (thread-safe in 16.x+)
private static readonly IDeserializer Deserializer = new DeserializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .IgnoreUnmatchedProperties()  // Handle extra fields gracefully
    .Build();

private static readonly ISerializer Serializer = new SerializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
    .Build();
```

**Frontmatter Extraction with Markdig:**
```csharp
var pipeline = new MarkdownPipelineBuilder()
    .UseYamlFrontMatter()
    .Build();

var document = Markdown.Parse(content, pipeline);
var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
var yaml = content.Substring(yamlBlock.Span.Start, yamlBlock.Span.Length);
```

---

### Implementation Phases (Simplified)

#### Phase 1: Core Loop (Foundation + Check-in)

**Tasks:**
- [ ] Create .NET 10 Avalonia project with simplified structure
- [ ] Implement `DataService` for reading/writing Pulse.md
- [ ] Create `CheckInPopup.axaml` with task checkboxes
- [ ] Implement `CheckInViewModel` with check/uncheck logic
- [ ] Implement `HourlyTimer` BackgroundService with PeriodicTimer
- [ ] Add basic tray icon with Quit option

**Files to create:**
- `src/Pulse/Pulse.csproj`
- `src/Pulse/Program.cs`
- `src/Pulse/App.axaml` / `App.axaml.cs`
- `src/Pulse/Models/PulseData.cs`, `Category.cs`
- `src/Pulse/Services/DataService.cs`
- `src/Pulse/Services/HourlyTimer.cs`
- `src/Pulse/ViewModels/CheckInViewModel.cs`
- `src/Pulse/Views/CheckInPopup.axaml` / `.axaml.cs`
- `src/Pulse/Views/MainWindow.axaml` / `.axaml.cs`

**Key Implementation: HourlyTimer with Sleep/Wake Handling**
```csharp
public class HourlyTimer : BackgroundService
{
    private PeriodicTimer? _timer;
    private DateTime _lastTickTime;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Align to next hour boundary
        var delay = GetDelayUntilNextHour();
        await Task.Delay(delay, stoppingToken);

        _lastTickTime = DateTime.Now;
        await ShowPopupOnUIThreadAsync();

        _timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await _timer.WaitForNextTickAsync(stoppingToken))
        {
            _lastTickTime = DateTime.Now;
            await ShowPopupOnUIThreadAsync();
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            // Check if we missed an hour boundary during sleep
            var currentHour = new DateTime(DateTime.Now.Year, DateTime.Now.Month,
                DateTime.Now.Day, DateTime.Now.Hour, 0, 0);
            if (_lastTickTime < currentHour)
            {
                Task.Run(ShowPopupOnUIThreadAsync);
            }
        }
    }

    private async Task ShowPopupOnUIThreadAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var popup = new CheckInPopup();
            popup.DataContext = new CheckInViewModel(_dataService);
            await popup.ShowDialog(GetMainWindow());
        });
    }

    private static TimeSpan GetDelayUntilNextHour()
    {
        var now = DateTime.Now;
        var nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0)
            .AddHours(1);
        return nextHour - now;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _timer?.Dispose();
        return base.StopAsync(cancellationToken);
    }
}
```

**Success Criteria:**
- Popup appears at the top of every hour
- Unchecking a task records its end time
- Done button saves to Pulse.md
- App survives system sleep and resumes correctly

#### Phase 2: Add Task + Recovery + Auto-Close

**Tasks:**
- [ ] Add inline "add task" panel to CheckInPopup (expandable)
- [ ] Implement recent tasks filtering (fuzzy search through 100)
- [ ] Add auto-fill from recent task selection
- [ ] Add 5-minute auto-close timer
- [ ] Implement recovery mode (conditional UI in same popup)
- [ ] "Yes, still going" / "No, I stopped" recovery buttons

**Key Implementation: Auto-Close Timer**
```csharp
public partial class CheckInViewModel : ObservableObject
{
    private readonly DispatcherTimer _autoCloseTimer;
    private const int AutoCloseMinutes = 5;

    public CheckInViewModel(DataService dataService)
    {
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(AutoCloseMinutes)
        };
        _autoCloseTimer.Tick += OnAutoClose;
        _autoCloseTimer.Start();
    }

    private void OnAutoClose(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();
        // Stop all active tasks
        foreach (var task in ActiveTasks)
        {
            task.EndTime = _popupOpenTime;
        }
        _dataService.SetMissedCheckIn(_popupOpenTime);
        _dataService.Save();
        RequestClose?.Invoke();
    }

    // Any user interaction resets the timer
    private void ResetAutoCloseTimer()
    {
        _autoCloseTimer.Stop();
        _autoCloseTimer.Start();
    }
}
```

**Success Criteria:**
- Text field searches through recent tasks
- Selecting recent task auto-fills description + category
- Popup auto-closes after 5 minutes, marks tasks stopped
- Recovery dialog appears after missed check-in

#### Phase 3: Day Rollover + Polish

**Tasks:**
- [ ] Implement day boundary detection
- [ ] Split tasks across days correctly
- [ ] Add tray menu: "Check In Now", "Pause Tracking"
- [ ] Manual check-in from tray
- [ ] Error handling and user-friendly messages
- [ ] Keyboard shortcuts (Enter = Done, Escape = Cancel)

**Key Implementation: Day Rollover**
```csharp
public void HandleDayRollover()
{
    var today = DateTime.Today;
    var yesterday = today.AddDays(-1);

    foreach (var task in _data.Active.ToList())
    {
        if (task.Started.Date < today)
        {
            // Close yesterday's entry at midnight
            AppendToLog(yesterday, task.Description, task.Category,
                task.Started.TimeOfDay, TimeSpan.FromHours(24));

            // Start new entry at midnight today
            task.Started = today; // 00:00
        }
    }
}
```

**Success Criteria:**
- Tasks at midnight appear in both days' logs
- "since HH:MM" and "until HH:MM" format correct
- Tray menu fully functional
- Error messages are helpful

---

### Security Considerations

### Research Insights: Security

**Path Validation (prevent traversal):**
```csharp
public static string SanitizePath(string configuredPath)
{
    string expandedPath = configuredPath.StartsWith("~/")
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                       configuredPath[2..])
        : configuredPath;

    string fullPath = Path.GetFullPath(expandedPath);
    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    if (!fullPath.StartsWith(homeDir, StringComparison.OrdinalIgnoreCase))
        throw new SecurityException("Data file must be within user home directory");

    if (!fullPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        throw new SecurityException("Data file must have .md extension");

    return fullPath;
}
```

**Atomic File Writes:**
```csharp
public async Task SaveAsync(PulseData data)
{
    var tempPath = _filePath + ".tmp";
    var content = Serialize(data);

    await File.WriteAllTextAsync(tempPath, content);
    File.Move(tempPath, _filePath, overwrite: true);
}
```

**YAML Size Limits:**
```csharp
const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
var fileInfo = new FileInfo(filePath);
if (fileInfo.Length > MaxFileSizeBytes)
    throw new SecurityException("Pulse.md exceeds maximum size");
```

**Task Description Sanitization:**
```csharp
public static string SanitizeDescription(string input)
{
    if (string.IsNullOrWhiteSpace(input)) return string.Empty;

    const int MaxLength = 500;
    var sanitized = input.Length > MaxLength ? input[..MaxLength] : input;
    sanitized = Regex.Replace(sanitized, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");
    return sanitized.Trim();
}
```

---

### Edge Cases & Error Handling

| Scenario | Handling |
|----------|----------|
| Pulse.md doesn't exist | Create with empty frontmatter |
| Pulse.md has invalid YAML | Show error, offer to reset or backup |
| File write fails (permissions) | Show error toast, don't lose data |
| App crash with active tasks | On next launch, trigger recovery |
| System time changes | Recalculate next hour boundary |
| Multiple hours missed (sleep) | Single recovery for all missed time |
| Symlink to sensitive file | Refuse to write, show error |

### Startup Behavior

```
App Launch
    │
    ▼
Load Pulse.md (or create if missing)
    │
    ├─ Has lastCheckIn and time since > 1 hour?
    │       │
    │       ▼
    │   Has missedCheckIn flag?
    │       │
    │       ├─ Yes: Show Recovery mode in popup
    │       └─ No: Show normal Check-in popup
    │
    └─ Time since < 1 hour?
            │
            ▼
        Wait for next hour boundary
```

---

## Acceptance Criteria

### Functional Requirements

- [ ] Popup appears every hour at the hour boundary (e.g., 14:00, 15:00)
- [ ] Active tasks displayed with checkboxes
- [ ] Unchecking a task stops it (records end time)
- [ ] New tasks can be added with description and category
- [ ] Recent tasks (100) are searchable/selectable
- [ ] Auto-close after 5 minutes marks all tasks as stopped
- [ ] Recovery dialog appears after missed check-in
- [ ] Day rollover correctly splits tasks across days
- [ ] All state persisted to single markdown file
- [ ] System tray icon with context menu

### Non-Functional Requirements

- [ ] Popup appears within 1 second of hour boundary
- [ ] File operations complete within 100ms
- [ ] Memory usage < 50MB idle
- [ ] Works on Linux with Wayland (via XWayland)
- [ ] Handles system sleep/wake gracefully

### Quality Gates

- [ ] All user flows manually tested
- [ ] Edge cases documented and handled
- [ ] Error messages are user-friendly
- [ ] Keyboard navigation works throughout

---

## Dependencies & Prerequisites

- .NET 10 SDK installed
- Linux with X11 or XWayland (for tray icon)
- Write access to `~/.config/pulse/` and `~/Me/Info/`

## Risk Analysis & Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Wayland tray icon issues | Medium | Medium | Fall back to no tray, rely on hourly popup |
| File corruption | Low | High | Atomic writes with temp file + rename |
| Timer drift | Low | Low | PeriodicTimer self-corrects; recalculate on sleep/wake |
| Avalonia Linux bugs | Medium | Medium | Test on target system early, report issues |
| UI thread deadlock | Medium | High | Always use `InvokeAsync`, never `.Wait()` |
| YAML deserialization attack | Low | Medium | Use strongly-typed deserialization only |

---

## References & Research

### Internal References
- Feature description: `DESCRIPTION.md`

### External References
- Avalonia UI Documentation: https://docs.avaloniaui.net/
- Avalonia TrayIcon: https://docs.avaloniaui.net/docs/reference/controls/tray-icon
- CommunityToolkit.Mvvm: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/
- YamlDotNet: https://github.com/aaubry/YamlDotNet
- Markdig: https://github.com/xoofx/markdig
- .NET Background Services: https://learn.microsoft.com/en-us/dotnet/core/extensions/workers
- PeriodicTimer: https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer
- SystemEvents.PowerModeChanged: https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.powermodechanged

### Key Patterns Referenced
- MVVM with CommunityToolkit source generators
- BackgroundService with PeriodicTimer for scheduling
- Power event handling for sleep/wake detection
- Markdig + YamlDotNet for frontmatter parsing
- Avalonia FluentTheme for cross-platform styling
- Atomic file writes for data integrity

---

## Open Questions (Resolved)

| Question | Resolution |
|----------|------------|
| File location | `~/Me/Info/Pulse.md` (configurable) |
| Categories | Fixed: Work, Hobby, Relationship, Other |
| Recent tasks limit | 100, LRU ordering |
| Auto-close timeout | 5 minutes |
| Check-in interval | 60 minutes (configurable) |
| Thread safety | YamlDotNet 16.x is thread-safe; reuse instances |
| System sleep | Handle via SystemEvents.PowerModeChanged |

## Out of Scope (Future Ideas)

- Analytics/charts/summaries
- Cloud sync
- Mobile app
- Custom categories
- Notification sounds
- Snooze button
- Export to other formats
