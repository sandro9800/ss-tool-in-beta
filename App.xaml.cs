using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AntiCheatScanner.Utils;

namespace AntiCheatScanner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        HandleException(e.Exception, "A fatal UI error occurred.");
        e.Handled = true;
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            HandleException(exception, "The application hit an unrecoverable error.");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        HandleException(e.Exception, "A background task failed.");
        e.SetObserved();
    }

    private static void HandleException(Exception exception, string message)
    {
        try
        {
            Logger.SaveCrash(exception);
        }
        catch
        {
        }

        MessageBox.Show(
            $"{message}{Environment.NewLine}{Environment.NewLine}{exception.Message}",
            "AntiCheat Scanner",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
