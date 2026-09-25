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
            if (source is null)
                source = $"{ex.TargetSite?.DeclaringType?.Assembly.FullName ?? "(unknown)"} : {ex.TargetSite?.DeclaringType?.FullName ?? "(unmanaged code)"}::{ex.TargetSite?.Name}";

            StringBuilder sb = new();
            sb.AppendLine(new string('-', 20));
            sb.AppendLine(CultureInfo.InvariantCulture, $"Source:    {source}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"Message:   {ex.Message}");

            if (ex.InnerException != null)
                sb.AppendLine(CultureInfo.InvariantCulture, $"Inner Message: {ex.InnerException.Message}");

            sb.AppendLine(new string('-', 20));

            Console.Error.WriteLine(sb.ToString());
        }
        catch
        {
            return;
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

        base.OnStartup(e);

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