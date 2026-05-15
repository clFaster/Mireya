using System;
using Avalonia.Controls;
using Mireya.Client.Avalonia.ViewModels;

namespace Mireya.Client.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Dispose the current view's ViewModel to stop timers, unhook events,
        // and release resources before the window closes.
        if (DataContext is MainWindowViewModel mainVm)
        {
            mainVm.Dispose();
        }

        base.OnClosing(e);
    }
}
