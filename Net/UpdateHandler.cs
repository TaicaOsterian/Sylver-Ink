using SylverInk.XAML;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using static SylverInk.FileIO.FileUtils;

namespace SylverInk.Net;

public static class UpdateHandler
{
    private static string GitReleasesURI { get; } = "https://api.github.com/repos/TaicaOsterian/Sylver-Ink/releases?per_page=1&page=1";
    public static bool GracefulExit { get; private set; }
    public static string TempUri { get; } = Path.Join(DocumentsFolder, "SylverInk.msi");
    public static string UpdateLockUri { get; } = Path.Join(DocumentsFolder, "~si_update.lock");
    private static CancellationTokenSource UpdateTokenSource { get; } = new();
    public static Update UpdateWindow { get; } = new();

    public static void CancelUpdate()
    {
        UpdateTokenSource.Cancel();
        GracefulExit = false;
    }

    public static async Task<bool> CheckForUpdates(bool notifyOnFail = false)
    {
        using var httpClient = new HttpClient();
        GracefulExit = false;
        Version? releaseVersion;
        string? uriNode = null;

        if (Assembly.GetExecutingAssembly().GetName().Version is not Version assemblyVersion)
            return false;

        if (Process.GetCurrentProcess().MainModule?.FileName is null)
            return false;

        // We apply a cadence of 14 days inbetween automatic updating.
        if (!notifyOnFail && DateTime.UtcNow.Subtract(DateTime.FromBinary(CommonUtils.Settings.LastUpdate)).TotalDays < 14.0)
        {
            GracefulExit = true;
            return true;
        }

        try
        {
            if (!httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("request"))
                return false;

            var jsonString = await httpClient.GetStringAsync(GitReleasesURI);
            if (JsonSerializer.Deserialize<JsonArray>(jsonString)?[0]?.AsObject() is not JsonObject release)
                return false;

            if (!release.TryGetPropertyValue("tag_name", out var tagNode) || !release.TryGetPropertyValue("assets", out var assetNode))
                return false;

            var releaseString = tagNode?.ToString() ?? "0.0.0";
            if (releaseString.StartsWith('v'))
                releaseString = releaseString[1..];

            if (!Version.TryParse(releaseString, out releaseVersion) || releaseVersion.CompareTo(assemblyVersion) < 1)
            {
                GracefulExit = true;
                return false;
            }

            if (assetNode?.AsArray() is not JsonArray assetArray)
                return false;

            foreach (var asset in assetArray)
            {
                if (asset is null)
                    continue;

                if (!asset.AsObject().TryGetPropertyValue("browser_download_url", out var nValue))
                    continue;

                if (nValue?.ToString() is not string nString)
                    continue;

                // Starting in v1.8.0: Download a .MSI package only if a .EXE package doesn't exist.
                if (nString.EndsWith(".exe", StringComparison.Ordinal))
                    uriNode = nString;
                else if (nString.EndsWith(".msi", StringComparison.Ordinal))
                    uriNode ??= nString;
            }

            if (uriNode is null)
            {
                GracefulExit = true;
                return false;
            }
        }
        catch (Exception e)
        {
            App.LogException(e);
            if (notifyOnFail)
                ShowTooltip(Strings.FailedUpdateCheck);

            GracefulExit = true;
            return true;
        }

        CommonUtils.Settings.LastUpdate = DateTime.UtcNow.ToBinary();

        if (MessageBox.Show(string.Format(
                CultureInfo.CurrentCulture,
                CacheMessageUpdateAvailable,
                assemblyVersion.ToString(3),
                releaseVersion.ToString(3)),
            Strings.Title_Notification,
            MessageBoxButton.YesNo,
            MessageBoxImage.Information) == MessageBoxResult.No)
        {
            if (!notifyOnFail)
            {
                ShowTooltip(Strings.OfferUpdateInHelp);
                CommonUtils.Settings.PromptForUpdate = false;
            }

            GracefulExit = true;
            return true;
        }

        await DownloadAndInstallUpdate(httpClient, uriNode);

        GracefulExit = true;
        return true;
    }

    private static async Task DownloadAndInstallUpdate(HttpClient httpClient, string uriNode)
    {
        try
        {
            Erase(TempUri);
            Erase(UpdateLockUri);

            File.Create(UpdateLockUri, 0).Close();

            UpdateWindow.Owner = Application.Current.MainWindow;
            UpdateWindow.Show();

            await httpClient.DownloadFileTaskAsync(uriNode, TempUri, UpdateTokenSource);

            if (UpdateTokenSource.IsCancellationRequested)
                return;

            AbortRun = true;

            Process.Start(new ProcessStartInfo()
            {
                Arguments = $"/c start /wait msiexec.exe /i \"{TempUri}\" /qb & start \"\" \"{Process.GetCurrentProcess().MainModule?.FileName}\"",
                CreateNoWindow = true,
                FileName = "cmd.exe",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            App.LogException(ex);

            if (ex is not OperationCanceledException)
                MessageBox.Show(string.Format(
                        CultureInfo.CurrentCulture,
                        CacheUnableToUpdate,
                        ex.Message),
                    Strings.Title_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

            GracefulExit = false;
            return;
        }
        finally
        {
            CommonUtils.Settings.PromptForUpdate = true;
            UpdateWindow?.Close();
        }
    }
}
