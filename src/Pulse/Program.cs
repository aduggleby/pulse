using System.Net.Sockets;
using Avalonia;

namespace Pulse;

class Program
{
    private static readonly string SocketPath = Path.Combine(
        Path.GetTempPath(),
        $"pulse-{Environment.UserName}.sock");

    [STAThread]
    public static void Main(string[] args)
    {
        // Try to signal existing instance
        if (TrySignalExistingInstance())
        {
            return; // Another instance is running, we signaled it to show
        }

        // We're the primary instance - start the app
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static bool TrySignalExistingInstance()
    {
        if (!File.Exists(SocketPath))
            return false;

        try
        {
            // Try to connect to existing instance
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            client.Connect(new UnixDomainSocketEndPoint(SocketPath));
            client.Send("show"u8);
            return true; // Successfully signaled existing instance
        }
        catch
        {
            // Stale socket file - clean it up
            try { File.Delete(SocketPath); } catch { }
            return false;
        }
    }

    public static void StartListening(Action onShowRequested)
    {
        // Clean up any stale socket file (shouldn't exist at this point, but just in case)
        try { File.Delete(SocketPath); } catch { }

        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
        listener.Listen(1);

        // Listen in background
        Task.Run(() =>
        {
            while (true)
            {
                try
                {
                    using var client = listener.Accept();
                    var buffer = new byte[16];
                    var received = client.Receive(buffer);
                    var message = System.Text.Encoding.UTF8.GetString(buffer, 0, received);

                    if (message == "show")
                    {
                        onShowRequested();
                    }
                }
                catch
                {
                    // Ignore errors, keep listening
                }
            }
        });
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
