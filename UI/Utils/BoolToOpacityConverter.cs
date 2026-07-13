using System;
using System.Globalization;
using System.Windows.Data;

namespace CQIng.Revit.ColumnasVigasMuros.UI.Utils;

/// <summary>
/// Convierte un booleano a un valor de opacidad para indicar visualmente que un control está deshabilitado.
/// true  → 1.0 (visible / habilitado)
/// false → 0.35 (semitransparente / deshabilitado)
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? 1.0 : 0.35;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
