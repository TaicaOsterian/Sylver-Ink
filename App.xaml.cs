using Microsoft.Win32;
using SylverInk.FileIO;
using System.Globalization;
using System.Text;
using System.Windows.Markup;
using System.Windows.Threading;

namespace SylverInk;

public partial class App : System.Windows.Application
{
    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogException(ex);

        MessageBox.Show(Strings.Message_CriticalError, Strings.Title_Error, MessageBoxButton.OK, MessageBoxImage.Error);
        Application.Current.Shutdown();
    }

    public static void LogException(Exception ex, string? source = null)
    {
        try
        {
            source ??= $"{ex.TargetSite?.DeclaringType?.Assembly.FullName ?? "(unknown)"} : {ex.TargetSite?.DeclaringType?.FullName ?? "(unmanaged code)"}::{ex.TargetSite?.Name}";

            StringBuilder sb = new();
            sb.AppendLine(new string('-', 20));
            sb.AppendLine(CultureInfo.InvariantCulture, $"Source:    {source}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Message:   {ex.Message}");

            if (ex.InnerException != null)
                sb.AppendLine(CultureInfo.InvariantCulture, $"Inner Message: {ex.InnerException.Message}");

            sb.AppendLine(new string('-', 20));

            Debug.WriteLine(sb.ToString());
        }
        catch
        {
            return;
        }
    }

    private async void OnSessionEnding(object sender, SessionEndingEventArgs e)
    {
        // Sylver Ink cannot guarantee that a save operation will take less than the provided five seconds during a soft reboot.
        // We accommodate as best as we can by briefly pausing the reboot and then re-initiating it when saving is done.

        e.Cancel = true;

        try
        {
            File.Create(FileUtils.SessionFile);
            Settings.Save();
            await DatabaseUtils.AutosaveDatabases();
        }
        catch (Exception ex)
        {
            LogException(ex);
        }
        finally
        {
            SystemEvents.SessionEnding -= OnSessionEnding;
            Process.Start("shutdown", "/r /t 0");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionEnding -= OnSessionEnding;
        base.OnExit(e);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

        base.OnStartup(e);

        SystemEvents.SessionEnding += OnSessionEnding;
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception, $"{e.Exception.TargetSite?.DeclaringType?.Assembly.FullName ?? "(unknown)"} : {e.Exception.TargetSite?.DeclaringType?.FullName ?? "(unmanaged code)"} (thread {e.Dispatcher.Thread.ManagedThreadId} \"{e.Dispatcher.Thread.Name}\")");
        e.Handled = true;
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException(e.Exception);
        e.SetObserved();
    }
}