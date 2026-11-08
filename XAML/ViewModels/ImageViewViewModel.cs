using Microsoft.Win32;
using SylverInk.Interop;
using SylverInk.Mvvm;

namespace SylverInk.XAML.ViewModels;

public class ImageViewViewModel : PopoutViewModel
{
    private ImageContainer? _source;
    private double _zoomLevel = 1.0;

    public ImageContainer? Source
    {
        get => _source;
        set
        {
            _source = value;
            OnPropertyChanged(null);
        }
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            _zoomLevel = value;
            OnPropertyChanged(null);
        }
    }

    public string ZoomLevelRender => $"{ZoomLevel:P0}";

    public ICommand ExportCommand { get; }

    public ImageViewViewModel()
    {
        ExportCommand = new RelayCommand(Export);
    }

    private void Export(object? param)
    {
        SaveFileDialog dialog = new()
        {
            AddToRecent = true,
            OverwritePrompt = true,
            Filter = Strings.FileFilter_Image,
            ValidateNames = true,
        };

        if (!dialog.ShowDialog() is true)
            return;

        var path = dialog.FileName;

        if (Source?.CompositedFrames.Count > 1 && !Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase)
            && MessageBox.Show(Strings.Message_ConfirmAnimatedImageExportLoss, Strings.Title_Warning, MessageBoxButton.YesNo) == MessageBoxResult.No)
            return;

        try
        {
            using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using StreamWriter writer = new(stream);
            writer.Write(Source?.ToByteArray());
            writer.Flush();
            writer.Close();
        }
        catch (Exception ex)
        {
            App.LogException(ex);
        }
    }
}
