using System.Windows;

namespace ChitterChatterClient;

/// <summary>
/// ChitterChatter Client Application
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Handle unhandled exceptions
        DispatcherUnhandledException += (sender, args) =>
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}",
                "ChitterChatter Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}
