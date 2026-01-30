using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Pulse.Views;

namespace Pulse;

public partial class App : Application
{
    private DispatcherTimer? _timer;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
        {
            // Show first check-in immediately
            ShowCheckInWindow();

            // Schedule next check-in at the hour boundary
            ScheduleNextCheckIn();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ScheduleNextCheckIn()
    {
        var now = DateTime.Now;
        var nextHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);
        var delay = nextHour - now;

        _timer = new DispatcherTimer { Interval = delay };
        _timer.Tick += OnHourlyTick;
        _timer.Start();
    }

    private void OnHourlyTick(object? sender, EventArgs e)
    {
        // Reset to exactly 1 hour for subsequent ticks
        _timer!.Interval = TimeSpan.FromHours(1);
        ShowCheckInWindow();
    }

    private void ShowCheckInWindow()
    {
        var window = new CheckInWindow();
        window.Show();
        window.Activate();
    }
}
