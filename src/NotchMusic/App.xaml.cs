using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Windows;

namespace NotchMusic;

public partial class App : System.Windows.Application
{
    private const string MutexName = "DRANOTCH_SINGLE_INSTANCE_MUTEX";
    private const string PipeName = "DRANOTCH_IPC_PIPE";
    private static Mutex? _mutex;
    private static bool _ownsMutex;
    private static CancellationTokenSource? _pipeCts;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (s, args) =>
        {
            args.Handled = true;
            var ex = args.Exception;
            var detail = ex.InnerException != null ? $"\n\nDetail: {ex.InnerException.Message}" : string.Empty;
            System.Windows.MessageBox.Show(
                $"Terjadi kesalahan aplikasi:\n{ex.Message}{detail}",
                "DRANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        };

        _mutex = new Mutex(true, MutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            // Already running! Notify existing instance through named pipe
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(1000);
                using var writer = new StreamWriter(client, Encoding.UTF8);
                writer.WriteLine("SHOW");
            }
            catch
            {
            }

            Shutdown();
            return;
        }

        // Start listening on named pipe
        StartPipeServer();

        base.OnStartup(e);
    }

    private void StartPipeServer()
    {
        _pipeCts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!_pipeCts.Token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync(_pipeCts.Token);
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var msg = await reader.ReadLineAsync();
                    if (msg == "SHOW")
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (MainWindow != null)
                            {
                                MainWindow.Show();
                                MainWindow.WindowState = WindowState.Normal;
                                MainWindow.Activate();
                            }
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pipeCts?.Cancel();
        if (_ownsMutex && _mutex != null)
        {
            try { _mutex.ReleaseMutex(); } catch { }
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
