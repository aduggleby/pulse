namespace Pulse.Models;

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
