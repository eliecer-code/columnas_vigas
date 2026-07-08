using System;
using System.Globalization;
using System.Windows.Data;

namespace CQIng.Revit.ColumnasVigasMuros.UI.Utils;

public class InverseBooleanConverter : IValueConverter
{
    public object Translate(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        return value;
    }
}
