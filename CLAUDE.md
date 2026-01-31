# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build
dotnet build

# Run in development
dotnet run --project src/Pulse

# Publish for deployment
dotnet publish -c Release -r linux-x64 --self-contained false -o dist/linux-x64

# Kill, publish, and restart (use this when deploying changes)
pkill -9 -f "Pulse" 2>/dev/null; rm -f /tmp/pulse-$USER.sock; dotnet publish -c Release -r linux-x64 --self-contained false -o dist/linux-x64 && ./dist/linux-x64/Pulse --background &
```

## Command Line Options

- `--background`: Start without showing the dialog immediately. App runs in background and shows dialog on the next hourly tick or when signaled by another instance.

## Architecture

Pulse is an Avalonia UI desktop app that pops up hourly to track what you're working on. Data is stored in a single markdown file with YAML frontmatter (`~/Me/Info/Pulse/Today.md`).

### Key Components

- **Program.cs**: Entry point with Unix socket singleton pattern. Prevents multiple instances; signals existing instance to show dialog if already running.
- **App.axaml.cs**: Application lifecycle. Schedules hourly check-ins, tracks current window to prevent duplicate dialogs.
- **Storage.cs**: Reads/writes `Today.md`. Handles YAML frontmatter parsing, day archiving, and Syncthing conflict cleanup.
- **Views/CheckInWindow**: Main popup UI. Shows active tasks, handles add/remove, auto-closes after 5 minutes of inactivity with countdown timer.
- **Models/**: `PulseState` (frontmatter data), `Category` enum, task models.

### Singleton Pattern

Uses Unix domain socket at `/tmp/pulse-{username}.sock`. New instances signal "show" to existing instance then exit. Stale sockets are cleaned up automatically.

### Data Flow

1. On startup/hourly tick: Load state from `Today.md` frontmatter
2. User interacts with popup
3. On Done/auto-close: Update frontmatter, append to day's log, archive previous day if needed

## Version Management

Version is in `src/Pulse/Pulse.csproj` (`<Version>` tag). Displayed in UI next to "PULSE" header. Bump version when deploying changes to verify correct build is running.
