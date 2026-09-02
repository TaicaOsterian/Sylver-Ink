using System.Windows.Controls.Primitives;
using static SylverInk.Interop.VisualUtils;

namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for Properties.xaml
/// </summary>
public partial class Properties : Window
{
    public PropertiesViewModel ViewModel => (PropertiesViewModel)DataContext;

    public Properties()
    {
        DataContext = new PropertiesViewModel();
        ViewModel.RequestClose += (_, _) => Close();
        InitializeComponent();
    }

    // Rewriting the calendar's entre control template just to alter the proportions of the header button would violate my religion.
    private void CalendarOpened(object sender, RoutedEventArgs e)
    {
        var popup = FindVisualChildByName<Popup>(sender as DependencyObject, "PART_Popup");
        var calendar = popup?.Child;
        var headerButton = FindVisualChildByName<Button>(calendar, "PART_HeaderButton");

        if (headerButton is null)
            return;

        headerButton.Height = 30;
        headerButton.Width = 120;

        return;
    }

    private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        ViewModel.InitializeCommand.Execute(null);
    }

    private void SelectTime(object? sender, RoutedEventArgs e)
    {
        TimeSelector.IsOpen = true;
    }
}