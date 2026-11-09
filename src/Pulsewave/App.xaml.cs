using System.Windows;

namespace Pulsewave;

public partial class App : System.Windows.Application
{
    private System.Threading.Mutex? _instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0 && e.Args[0] == ClaudeStatusLine.Argument)
        {
            // Claude Code's status line command: JSON in, one line out, no window and no second HUD.
            var utf8 = new System.Text.UTF8Encoding(false);
            using (var input = new System.IO.StreamReader(Console.OpenStandardInput(), utf8))
            using (var output = new System.IO.StreamWriter(Console.OpenStandardOutput(), utf8))
                output.Write(ClaudeStatusLine.Relay(input.ReadToEnd(), ClaudeStatusLine.UsageFile));
            Shutdown();
            return;
        }
        // Keep the old mutex name so an older install and this renamed build cannot overlap.
        _instance = new System.Threading.Mutex(true, "Local\\GraphiteMonitor-" + Environment.UserName, out bool first);
        if (!first) { Shutdown(); return; }
        var window = new MainWindow();
        MainWindow = window;
        window.Closed += (_, _) => Shutdown();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
