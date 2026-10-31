namespace SylverInk.XAML;

/// <summary>
/// Interaction logic for ImageView.xaml
/// </summary>
public partial class ImageView : Popout
{
    public override ImageViewViewModel ViewModel => (ImageViewViewModel)ViewModelProperty;

    public ImageView()
    {
        DataContext = new ImageViewViewModel();
        base.Construct((ImageViewViewModel)DataContext);
        ViewModel.RequestClose += (_, _) => HandleClose();
        InitializeComponent();
    }

    public void Construct(ImageSource source)
    {
        ViewModel.Source = source;
        OpenImages.Add(this);
    }

    private void HandleClose()
    {
        OpenImages.Remove(this);
        Close();
    }
}
