using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Pulse.Views;

namespace Pulse;

public partial class App : Application
{
    private DispatcherTimer? _timer;
    private CheckInWindow? _currentWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Keep app running after window closes
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Listen for signals from other instances
            Program.StartListening(() =>
            {
                Dispatcher.UIThread.Post(ShowCheckInWindow);
            });

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
        // If window already exists and is visible, just activate it
        if (_currentWindow is { IsVisible: true })
        {
            _currentWindow.Activate();
            return;
        }

        _currentWindow = new CheckInWindow();
        _currentWindow.Closed += (_, _) => _currentWindow = null;
        _currentWindow.Show();
        _currentWindow.Activate();
    }
}
