using System.Windows;
using System.Windows.Input;
using ChitterChatterClient.ViewModels;

namespace ChitterChatterClient;

/// <summary>
/// ChitterChatter main window.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Handle push-to-talk key events
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;

        // Clean up on close
        Closing += OnClosing;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        // Push-to-talk with Space key (only when enabled and not in a text box)
        if (e.Key == Key.Space && 
            ViewModel?.UsePushToTalk == true && 
            !IsTextBoxFocused())
        {
            ViewModel.SetPushToTalk(true);
            e.Handled = true;
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && ViewModel?.UsePushToTalk == true)
        {
            ViewModel.SetPushToTalk(false);
            e.Handled = true;
        }
    }

    private static bool IsTextBoxFocused()
    {
        return Keyboard.FocusedElement is System.Windows.Controls.TextBox;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        ViewModel?.Dispose();
    }
}
