using System.IO.Pipes;
using System.Net.Sockets;
using Avalonia;

namespace Pulse;

class Program
{
    private const string AppId = "Pulse";

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
        if (OperatingSystem.IsWindows())
            return TrySignalWindows();
        else
            return TrySignalUnix();
    }

    public static void StartListening(Action onShowRequested)
    {
        if (OperatingSystem.IsWindows())
            StartListeningWindows(onShowRequested);
        else
            StartListeningUnix(onShowRequested);
    }

    // ===== Windows: Named Pipes =====

    private static bool TrySignalWindows()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", AppId, PipeDirection.Out);
            client.Connect(100); // 100ms timeout
            client.Write("show"u8);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void StartListeningWindows(Action onShowRequested)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(AppId, PipeDirection.In);
                    await server.WaitForConnectionAsync();

                    var buffer = new byte[16];
                    var read = await server.ReadAsync(buffer);
                    var message = System.Text.Encoding.UTF8.GetString(buffer, 0, read);

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

    // ===== Unix: Domain Sockets =====

    private static readonly string SocketPath = Path.Combine(
        Path.GetTempPath(),
        $"pulse-{Environment.UserName}.sock");

    private static bool TrySignalUnix()
    {
        if (!File.Exists(SocketPath))
            return false;

        try
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            client.Connect(new UnixDomainSocketEndPoint(SocketPath));
            client.Send("show"u8);
            return true;
        }
        catch
        {
            // Stale socket file - clean it up
            try { File.Delete(SocketPath); } catch { }
            return false;
        }
    }

    private static void StartListeningUnix(Action onShowRequested)
    {
        // Clean up any stale socket file
        try { File.Delete(SocketPath); } catch { }

        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
        listener.Listen(1);

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
