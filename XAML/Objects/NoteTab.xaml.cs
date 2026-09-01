using SylverInk.Notes;
using SylverInk.XAML.Objects.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SylverInk.XAML.Objects;

/// <summary>
/// Interaction logic for NoteTab.xaml
/// </summary>
public partial class NoteTab : UserControl
{
    public NoteTabViewModel ViewModel => (NoteTabViewModel)DataContext;

    public NoteTab()
    {
        DataContext = new NoteTabViewModel();
        ViewModel.RequestCloseSearchPopup += (_, _) => InternalSearchPopup.IsOpen = false;
        InitializeComponent();
    }

    private void ISPText_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 ||
                    (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                {
                    ViewModel.FindPreviousCommand.Execute(null);
                    ISPText.Focus();
                    break;
                }

                ViewModel.FindNextCommand.Execute(null);
                ISPText.Focus();
                break;
            case Key.Escape:
                InternalSearchPopup.IsOpen = false;
                break;
        }
    }

    private void NoteBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.TextChanged();
        UpdateTextColorButton();
    }

    private void NoteTab_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            InternalSearchPopup.IsOpen = true;
            ISPText.Focus();

            e.Handled = true;
        }
    }

    private void NoteTab_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Construct();
        TextColorButton.Background = CommonUtils.Settings.ListForeground;
        TextColorPicker.Init(NoteBox);
    }

    private void NoteBox_SelectionChanged(object sender, RoutedEventArgs e) => UpdateTextColorButton();

    public void RequestUnlock(NoteRecord record) => ViewModel.RequestUnlock(record);

    private void SelectColor(object? sender, RoutedEventArgs e)
    {
        TextColorPicker.ColorTag = "PT";
        TextColorPicker.ColorSelection.IsOpen = true;
    }

    private void UpdateTextColorButton()
    {
        TextColorButton.Background = NoteBox.Selection.End.Parent.GetValue(TextElement.ForegroundProperty) as Brush ?? CommonUtils.Settings.ListForeground;
    }
}
