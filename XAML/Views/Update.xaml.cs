namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for Update.xaml
/// </summary>
public partial class Update : Window
{
    internal UpdateViewModel ViewModel => (UpdateViewModel)DataContext;

    internal Update()
    {
        DataContext = new UpdateViewModel();
        InitializeComponent();
    }

    private void Drag(object? sender, MouseButtonEventArgs e) => DragMove();
}
