# Pulse

A minimal hourly time tracker that helps you stay aware of how you spend your day.

## Overview

Pulse is a desktop app that pops up every hour and asks "What are you doing?" You check off active tasks, add new ones, and it quietly logs everything to a markdown file. No complex dashboards, no cloud sync — just honest time awareness.

## Core Behavior

### Hourly Check-in
- Popup appears once per hour (configurable interval)
- Shows on the current monitor/workspace
- Displays currently active tasks with checkboxes
- Allows adding new tasks or marking existing ones as stopped

### Task Model
Each task has:
- **Description**: Free-text (e.g., "Working on Pulse app")
- **Category**: One of `Work`, `Hobby`, `Relationship`, `Other`
- **Status**: Active or Stopped
- **Start Hour**: When it began (e.g., "14:00")
- **End Hour**: When it stopped (nullable if ongoing)

### Parallel Tasks
- Multiple tasks can be active simultaneously
- User can check/uncheck tasks to indicate what's currently happening
- Unchecking a task marks its end time as the current hour

### Day Rollover
- If a task is still active at midnight (or first check-in of new day), it gets duplicated to the new day's entry
- The previous day's entry gets an end time of "24:00" (or "00:00" next day)
- The new day's entry starts at "00:00"

## Data Storage

### File Location
- Default: `~/Me/Info/Pulse.md`
- Configurable via settings

### Markdown Format
```markdown
---
active:
  - description: Code review
    category: Work
    started: 2026-01-30T14:00
recent:
  - description: Writing Pulse spec
    category: Work
  - description: Code review
    category: Work
  - description: Reading
    category: Hobby
lastCheckIn: 2026-01-30T15:00
missedCheckIn: null
---

# Pulse Log

## 2026-01-30

- [Work] Writing Pulse spec (09:00 - 17:00)
- [Hobby] Listening to music (09:00 - 12:00)
- [Work] Code review (14:00 - ongoing)
- [Relationship] Dinner with Sarah (18:00 - 20:00)

## 2026-01-29

- [Work] Client project (10:00 - 18:00)
- [Hobby] Reading (since 20:00)
```

**Frontmatter Fields:**
- `active`: Currently running tasks (with full start timestamp)
- `recent`: Last 100 unique task descriptions + categories (for quick-add search)
- `lastCheckIn`: Timestamp of last successful check-in
- `missedCheckIn`: Timestamp if auto-close happened (null otherwise)

Obsidian will display these as document properties and ignore them in reading view.

### Cross-Day Tasks
When a task spans multiple days, show partial times:
```markdown
## 2026-01-30
- [Work] Long debugging session (until 03:00)
- [Hobby] Reading (09:00 - 22:00)

## 2026-01-29
- [Work] Long debugging session (since 22:00)
```

- `since HH:MM` = Started this day, continued to next
- `until HH:MM` = Continued from previous day, ended this time
- `ongoing` = Currently active

## User Interface

### Check-in Popup
```
┌─────────────────────────────────────┐
│  What are you doing?          15:00 │
├─────────────────────────────────────┤
│  ☑ Writing Pulse spec        [Work] │
│  ☐ Listening to music       [Hobby] │
│  ☑ Code review               [Work] │
├─────────────────────────────────────┤
│  [+ Add Task]                       │
├─────────────────────────────────────┤
│              [Done]                 │
└─────────────────────────────────────┘
```

### Add Task Dialog
```
┌─────────────────────────────────────┐
│  New Task                           │
├─────────────────────────────────────┤
│  What are you doing?                │
│  [________________________]  🔍     │
│                                     │
│  ┌─ Recent tasks ────────────────┐  │
│  │ Writing Pulse spec     [Work] │  │
│  │ Code review            [Work] │  │
│  │ Reading               [Hobby] │  │
│  └───────────────────────────────┘  │
│                                     │
│  Category:                          │
│  (•) Work  ( ) Hobby                │
│  ( ) Relationship  ( ) Other        │
├─────────────────────────────────────┤
│         [Cancel]  [Add]             │
└─────────────────────────────────────┘
```

- Text field searches through last 100 unique task descriptions
- Selecting a recent task auto-fills description AND category
- Typing filters the list in real-time

### Auto-Close Behavior
- If popup is open for **5 minutes** without user action, it auto-closes
- All currently active tasks are **stopped** (end time = popup open time)
- State is saved as "missed check-in"

### Missed Check-in Recovery
Next time the popup appears after a missed check-in:
```
┌─────────────────────────────────────┐
│  Welcome back!                16:00 │
├─────────────────────────────────────┤
│  You missed the 15:00 check-in.     │
│  Were you still working on these?   │
│                                     │
│  ☑ Writing Pulse spec        [Work] │
│  ☑ Code review               [Work] │
│                                     │
│  [No, I stopped]  [Yes, still going]│
└─────────────────────────────────────┘
```

- **"Yes, still going"**: Tasks are treated as if they were never stopped (continuous from before the missed check-in)
- **"No, I stopped"**: Tasks remain stopped at the missed check-in time, user can add new tasks

## Configuration

Settings stored in `~/.config/pulse/settings.json`:

```json
{
  "dataFile": "~/Me/Info/Pulse.md",
  "intervalMinutes": 60,
  "autoCloseMinutes": 5,
  "categories": ["Work", "Hobby", "Relationship", "Other"],
  "recentTasksLimit": 100,
  "autoStart": true
}
```

## Technical Stack

- **Language**: C# / .NET 10
- **UI Framework**: Avalonia UI (cross-platform, Wayland-native)
- **Scheduling**: Background service with timer (or systemd user timer)
- **Data**: Plain markdown file (human-readable, git-friendly)

## Internal State

All state is stored in the **YAML frontmatter** of `Pulse.md` itself — no separate state file needed.

This means:
- Single source of truth
- Human-readable and editable
- Works with Obsidian (shows as document properties)
- Git-friendly (one file to track)

The app reads frontmatter on startup, updates it on every check-in.

## Non-Goals

- No analytics or charts (for now)
- No cloud sync
- No mobile app
- No complex project hierarchies

## Future Ideas (Not MVP)

- Weekly/monthly summary view
- Custom categories
- Notification sound options
- "Snooze" button
- Export to other formats
