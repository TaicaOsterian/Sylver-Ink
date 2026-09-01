using System.ComponentModel;
using System.Runtime.CompilerServices;
using static SylverInk.CommonUtils;

namespace SylverInk.XAMLUtils;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public static ContextSettings AppSettings => CommonUtils.Settings;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        try
        {
            if (PropertyChanged is null)
                return;

            Concurrent(PropertyChanged.Invoke, this, new PropertyChangedEventArgs(name));
        }
        catch
        {
            // The most common cause of an exception here is a property changing while the application is shutting down.
            return;
        }
    }
}
