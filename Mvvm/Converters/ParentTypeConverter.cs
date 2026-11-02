using System.Globalization;
using System.Windows.Data;

namespace SylverInk.Mvvm.Converters;

public class ParentTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        Type? targetParentType = parameter as Type;
        if (targetParentType is null)
            return value.GetType().Name == parameter.ToString();

        return value.GetType() == targetParentType || value.GetType().IsSubclassOf(targetParentType);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
