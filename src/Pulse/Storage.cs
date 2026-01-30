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

    private readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private readonly ISerializer _yamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public Storage()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _pulsePath = Path.Combine(home, "Me", "Info", "Pulse.md");
        _archiveDir = Path.Combine(home, "Me", "Info", "pulse");
    }

    public (PulseState State, string TodayLog) Load()
    {
        if (!File.Exists(_pulsePath))
        {
            return (new PulseState(), "");
        }

        var content = File.ReadAllText(_pulsePath);
        var (frontmatter, body) = ParseFrontmatter(content);

        var state = string.IsNullOrWhiteSpace(frontmatter)
            ? new PulseState()
            : _yamlDeserializer.Deserialize<PulseState>(frontmatter) ?? new PulseState();

        return (state, body.Trim());
    }

    public void Save(PulseState state, string todayLog)
    {
        // Check if day changed - archive if needed
        if (state.LastCheckIn?.Date < DateTime.Today)
        {
            ArchiveDay(state.LastCheckIn.Value.Date, todayLog);
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
        var content = $"# {date:yyyy-MM-dd}\n\n{dayLog}";
        File.WriteAllText(archivePath, content);
    }

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
        var line = $"- [{task.Category}] {task.Description} ({task.Started:HH:mm} - {endTime:HH:mm})";
        return string.IsNullOrWhiteSpace(currentLog)
            ? line
            : currentLog + "\n" + line;
    }

    public string AppendOngoingToLog(string currentLog, ActiveTask task)
    {
        var line = $"- [{task.Category}] {task.Description} ({task.Started:HH:mm} - ongoing)";
        return string.IsNullOrWhiteSpace(currentLog)
            ? line
            : currentLog + "\n" + line;
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
