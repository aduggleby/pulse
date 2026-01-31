using System.Text;
using System.Text.RegularExpressions;
using Pulse.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Pulse;

public partial class Storage
{
    private readonly string _pulsePath;
    private readonly string _archiveDir;
    private const int MaxRecentTasks = 100;

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "pulse", "settings.json");

    private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly ISerializer _yamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public string DataDirectory { get; }

    public Storage()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        DataDirectory = LoadDataDirectory() ?? Path.Combine(home, "pulse");

        _pulsePath = Path.Combine(DataDirectory, "Today.md");
        _archiveDir = Path.Combine(DataDirectory, "Archive");
    }

    private static string? LoadDataDirectory()
    {
        if (!File.Exists(ConfigPath))
            return null;

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var config = System.Text.Json.JsonSerializer.Deserialize<PulseConfig>(json, options);
            if (!string.IsNullOrWhiteSpace(config?.DataDirectory))
            {
                // Expand ~ to home directory
                var path = config.DataDirectory;
                if (path.StartsWith("~/"))
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    path = Path.Combine(home, path[2..]);
                }
                return path;
            }
        }
        catch
        {
            // Ignore config errors, use default
        }
        return null;
    }

    public static void SaveDataDirectory(string path)
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);

        // Collapse home directory to ~
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith(home))
        {
            path = "~" + path[home.Length..];
        }

        var config = new PulseConfig { DataDirectory = path };
        var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(ConfigPath, json);
    }

    private class PulseConfig
    {
        public string? DataDirectory { get; set; }
    }

    private void CleanupSyncConflicts()
    {
        // Clean up Syncthing conflict files in the Pulse directories
        var directories = new[] { Path.GetDirectoryName(_pulsePath)!, _archiveDir };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
                continue;

            var conflictFiles = Directory.GetFiles(dir, "*.sync-conflict-*");
            foreach (var file in conflictFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }
        }
    }

    public (PulseState State, string TodayLog) Load()
    {
        CleanupSyncConflicts();

        if (!File.Exists(_pulsePath))
        {
            return (new PulseState(), "");
        }

        var content = File.ReadAllText(_pulsePath);
        var (frontmatter, body) = ParseFrontmatter(content);

        var state = string.IsNullOrWhiteSpace(frontmatter)
            ? new PulseState()
            : _yamlDeserializer.Deserialize<PulseState>(frontmatter) ?? new PulseState();

        // Ensure lists are not null (YAML can deserialize null)
        state.Categories ??= ["Work", "Hobby", "Relationship", "Other"];
        state.Active ??= [];
        state.Recent ??= [];

        // Strip headers, keep only log entries (lines starting with "- ")
        var logEntries = body
            .Split('\n')
            .Where(line => line.StartsWith("- "))
            .ToList();

        var todayLog = string.Join("\n", logEntries);

        // Archive previous day's log if day changed (before returning to caller)
        if (state.LastCheckIn?.Date < DateTime.Today && !string.IsNullOrWhiteSpace(todayLog))
        {
            ArchiveDay(state.LastCheckIn.Value.Date, todayLog);
            todayLog = "";  // Start fresh for today
        }

        return (state, todayLog);
    }

    public void Save(PulseState state, string todayLog, DateTime? previousCheckIn = null)
    {
        CleanupSyncConflicts();

        // Check if day changed - archive if needed (use previousCheckIn if provided)
        var checkDate = previousCheckIn ?? state.LastCheckIn;
        if (checkDate?.Date < DateTime.Today)
        {
            ArchiveDay(checkDate.Value.Date, todayLog);
            todayLog = "";  // Reset for new day
        }

        // Ensure directory exists
        var dir = Path.GetDirectoryName(_pulsePath)!;
        Directory.CreateDirectory(dir);

        // Build content
        var yaml = _yamlSerializer.Serialize(state);
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.Append(yaml);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("# Pulse Log");
        sb.AppendLine();
        sb.AppendLine($"## {DateTime.Today:yyyy-MM-dd}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(todayLog))
        {
            sb.AppendLine(todayLog);
        }

        // Atomic write
        var tempPath = _pulsePath + ".tmp";
        File.WriteAllText(tempPath, sb.ToString());
        File.Move(tempPath, _pulsePath, overwrite: true);
    }

    private void ArchiveDay(DateTime date, string dayLog)
    {
        if (string.IsNullOrWhiteSpace(dayLog))
            return;

        Directory.CreateDirectory(_archiveDir);

        var archivePath = Path.Combine(_archiveDir, $"{date:yyyy-MM-dd}.md");
        var summary = GenerateDaySummary(dayLog);
        var content = $"# {date:yyyy-MM-dd}\n\n{summary}\n## Log\n\n{dayLog}";
        File.WriteAllText(archivePath, content);
    }

    private string GenerateDaySummary(string dayLog)
    {
        var taskTimes = new Dictionary<string, TimeSpan>();
        var timeRangeRegex = TimeRangeRegex();

        foreach (var line in dayLog.Split('\n'))
        {
            if (!line.StartsWith("- ["))
                continue;

            // Extract task name (everything between ] and ()
            var bracketEnd = line.IndexOf(']');
            var parenStart = line.LastIndexOf('(');
            if (bracketEnd < 0 || parenStart < 0 || parenStart <= bracketEnd)
                continue;

            var taskName = line[(bracketEnd + 2)..parenStart].Trim();
            var timesPart = line[(parenStart + 1)..].TrimEnd(')');

            var totalTime = TimeSpan.Zero;
            foreach (Match match in timeRangeRegex.Matches(timesPart))
            {
                var startTime = TimeSpan.Parse(match.Groups[1].Value);
                var endStr = match.Groups[2].Value;

                if (endStr == "ongoing")
                    continue;

                var endTime = TimeSpan.Parse(endStr);
                var duration = endTime - startTime;
                if (duration < TimeSpan.Zero)
                    duration += TimeSpan.FromHours(24); // Handle midnight crossing

                totalTime += duration;
            }

            if (totalTime > TimeSpan.Zero)
            {
                if (taskTimes.ContainsKey(taskName))
                    taskTimes[taskName] += totalTime;
                else
                    taskTimes[taskName] = totalTime;
            }
        }

        if (taskTimes.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("## Summary\n");
        foreach (var (task, time) in taskTimes.OrderByDescending(x => x.Value))
        {
            var hours = (int)time.TotalHours;
            var minutes = time.Minutes;
            var timeStr = hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
            sb.AppendLine($"- **{task}**: {timeStr}");
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"(\d{2}:\d{2})\s*-\s*(\d{2}:\d{2}|ongoing)")]
    private static partial Regex TimeRangeRegex();

    public void AddToRecent(PulseState state, RecentTask task)
    {
        // Remove if already exists (will re-add at front)
        state.Recent.RemoveAll(r =>
            r.Description.Equals(task.Description, StringComparison.OrdinalIgnoreCase));

        // Add to front
        state.Recent.Insert(0, task);

        // Trim to max
        if (state.Recent.Count > MaxRecentTasks)
        {
            state.Recent.RemoveRange(MaxRecentTasks, state.Recent.Count - MaxRecentTasks);
        }
    }

    public string AppendToLog(string currentLog, ActiveTask task, DateTime endTime)
    {
        var timeRange = $"{task.Started:HH:mm} - {endTime:HH:mm}";
        return AppendTimeToLog(currentLog, task.Category, task.Description, timeRange);
    }

    public string AppendOngoingToLog(string currentLog, ActiveTask task)
    {
        var timeRange = $"{task.Started:HH:mm} - ongoing";
        return AppendTimeToLog(currentLog, task.Category, task.Description, timeRange);
    }

    private string AppendTimeToLog(string currentLog, Category category, string description, string timeRange)
    {
        if (string.IsNullOrWhiteSpace(currentLog))
        {
            return $"- [{category}] {description} ({timeRange})";
        }

        // Look for existing entry with same category and description
        var prefix = $"- [{category}] {description} (";
        var lines = currentLog.Split('\n').ToList();

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                // Found existing entry - append time range
                // Remove trailing ) and add new time range
                lines[i] = lines[i].TrimEnd(')') + $", {timeRange})";
                return string.Join("\n", lines);
            }
        }

        // No existing entry - add new line
        return currentLog + "\n" + $"- [{category}] {description} ({timeRange})";
    }

    private static (string Frontmatter, string Body) ParseFrontmatter(string content)
    {
        var match = FrontmatterRegex().Match(content);
        if (!match.Success)
            return ("", content);

        return (match.Groups[1].Value, match.Groups[2].Value);
    }

    [GeneratedRegex(@"^---\s*\n(.*?)\n---\s*\n?(.*)", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();
}
