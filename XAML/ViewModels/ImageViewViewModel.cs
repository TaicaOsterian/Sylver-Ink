using Microsoft.Win32;
using SylverInk.Mvvm;
using System.Windows.Media.Imaging;

namespace SylverInk.XAML.ViewModels;

public class ImageViewViewModel : PopoutViewModel
{
    private ImageSource? _source;

    public ImageSource? Source
    {
        get => _source;
        set
        {
            _source = value;
            OnPropertyChanged();
        }
    }

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
            Filter = Strings.ImageFileFilter,
            ValidateNames = true,
        };

        if (!dialog.ShowDialog() is true)
            return;

        var path = dialog.FileName;

        using FileStream stream = new(path, FileMode.Create);
        BitmapEncoder encoder;

        switch (Path.GetExtension(path))
        {
            case ".jpg":
            case ".jpeg":
                encoder = new JpegBitmapEncoder()
                {
                    QualityLevel = 100,
                };
                break;
            case ".png":
                encoder = new PngBitmapEncoder()
                {
                    Interlace = PngInterlaceOption.On
                };
                break;
            default:
                ShowTooltip(Strings.FileFormatUnsupported);
                return;
        }

        encoder.Frames.Add(BitmapFrame.Create(Source as BitmapSource));
        encoder.Save(stream);
    }
}
