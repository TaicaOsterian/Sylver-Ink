using SylverInk.Mvvm;
using System.Windows.Controls.Primitives;
using static SylverInk.Notes.DatabaseUtils;

namespace SylverInk.XAML.Menus;

public class NetworkMenuViewModel : ViewModelBase
{
    private string _addressCode = string.Empty;
    private string _addressCodeDisplay = string.Empty;
    private bool _codePopupVisible;
    private bool _connectPopupVisible;

    public string AddressCode
    {
        get => _addressCode;
        set
        {
            _addressCode = value;
            OnPropertyChanged();
        }
    }

    public string AddressCodeDisplay
    {
        get => _addressCodeDisplay;
        set
        {
            _addressCodeDisplay = value;
            OnPropertyChanged();
        }
    }

    public bool CodePopupVisible
    {
        get => _codePopupVisible;
        set
        {
            _codePopupVisible = value;
            OnPropertyChanged();
        }
    }

    public bool ConnectPopupVisible
    {
        get => _connectPopupVisible;
        set
        {
            _connectPopupVisible = value;
            OnPropertyChanged();
        }
    }

    public ICommand CancelConnectCommand { get; }
    public ICommand CloseCodePopupCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand ConnectPopupEnterCommand { get; }
    public ICommand ConnectPopupEscapeCommand { get; }
    public ICommand CopyAddressCodeCommand { get; }
    public ICommand CopyCodeCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand SaveConnectCommand { get; }
    public ICommand ServeCommand { get; }
    public ICommand UnserveCommand { get; }

    public NetworkMenuViewModel()
    {
        CancelConnectCommand = new RelayCommand(_ => ConnectPopupVisible = false);
        CloseCodePopupCommand = new RelayCommand(_ => CodePopupVisible = false);
        ConnectCommand = new RelayCommand(MenuConnect, CanConnect);
        ConnectPopupEnterCommand = new RelayCommand(PopupSaveAddress);
        ConnectPopupEscapeCommand = new RelayCommand(_ => ConnectPopupVisible = false);
        CopyAddressCodeCommand = new RelayCommand(PopupCodeClosed);
        CopyCodeCommand = new RelayCommand(MenuCopyCode, CanCopyCode);
        DisconnectCommand = new RelayCommand(MenuDisconnect, CanDisconnect);
        SaveConnectCommand = new RelayCommand(PopupSaveAddress);
        ServeCommand = new RelayCommand(MenuServe, CanServe);
        UnserveCommand = new RelayCommand(MenuUnserve, CanUnserve);
    }

    private static bool CanConnect(object? param) => !CurrentDatabase.Client.Active && !CurrentDatabase.Server.Active;

    private static bool CanCopyCode(object? param) => !CurrentDatabase.Client.Active && CurrentDatabase.Server.Active;

    private static bool CanDisconnect(object? param) => CurrentDatabase.Client.Active && !CurrentDatabase.Server.Active;

    private static bool CanServe(object? param) => !CurrentDatabase.Client.Active && !CurrentDatabase.Server.Active;

    private static bool CanUnserve(object? param) => !CurrentDatabase.Client.Active && CurrentDatabase.Server.Active;

    private static void CopyCode()
    {
        try
        {
            Clipboard.SetText(CurrentDatabase.Server?.AddressCode);
        }
        catch (Exception e)
        {
            App.LogException(e);
            ShowTooltip(Strings.Tooltip_FailedCopy);
        }
    }

    private static void MenuCopyCode(object? param)
    {
        CopyCode();
    }

    private void MenuConnect(object? param)
    {
        ConnectPopupVisible = true;
        AddressCode = string.Empty;
    }

    private static void MenuDisconnect(object? param)
    {
        CurrentDatabase.Client.Disconnect();
        CurrentDatabase.Changed = true;
    }

    private async void MenuServe(object? param)
    {
        if (await CurrentDatabase.Server.Serve(0) is false)
            return;

        if (Application.Current.MainWindow.FindName("CodePopup") is not Popup codePopup)
            return;

        if (codePopup.FindName("CodeBox") is not TextBox codeBox)
            return;

        codePopup.IsOpen = true;
        codeBox.Text = AddressCode ?? NetworkUtils.LoopbackCode;
    }

    private static void MenuUnserve(object? param) => CurrentDatabase.Server.Close();

    private void PopupCodeClosed(object? param)
    {
        if (CurrentDatabase == null)
            return;

        CopyCode();
        CodePopupVisible = false;
    }

    private async void PopupSaveAddress(object? param)
    {
        ConnectPopupVisible = false;

        if (string.IsNullOrWhiteSpace(AddressCode) || AddressCode.Length != 6)
        {
            MessageBox.Show(Strings.Message_InvalidAddressCode, Strings.Title_Error, MessageBoxButton.OK);
            return;
        }

        Database newDB = new();
        AddDatabase(newDB);
        await newDB.Client.Connect(AddressCode);
    }
}
