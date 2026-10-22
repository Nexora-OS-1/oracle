using System.Windows;
namespace OracleApp;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            try { CoreChecks.Run(); Console.WriteLine("Core checks passed."); Environment.ExitCode = 0; }
            catch (Exception ex) { Console.Error.WriteLine("Core checks failed: " + ex.Message); Environment.ExitCode = 1; }
            Shutdown(); return;
        }
        new MainWindow().Show();
    }
}
