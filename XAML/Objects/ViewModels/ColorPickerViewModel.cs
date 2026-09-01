using static SylverInk.Interop.VisualUtils;

namespace SylverInk.XAML.Objects.ViewModels;

public class ColorPickerViewModel : ViewModelBase
{
    private Brush _customColor = Brushes.Transparent;
    private string _customColorCode = "00000000";
    private bool _isColorGridOpen;
    private bool _isCustomSelectionOpen;

    public Brush CustomColor
    {
        get => _customColor;
        set
        {
            _customColor = value;
            OnPropertyChanged();
        }
    }

    public string CustomColorCode
    {
        get => _customColorCode;
        set
        {
            CustomColor = BrushFromBytes(value);
            _customColorCode = value;
            OnPropertyChanged();
        }
    }

    public bool IsColorGridOpen
    {
        get => _isColorGridOpen;
        set
        {
            _isColorGridOpen = value;
            OnPropertyChanged();
        }
    }

    public bool IsCustomSelectionOpen
    {
        get => _isCustomSelectionOpen;
        set
        {
            _isCustomSelectionOpen = value;
            OnPropertyChanged();
        }
    }

    public ColorPickerViewModel()
    {
    }
}
