using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FactuurWizard.App.Converters;

/// <summary>DateOnly(?) in het model ↔ DateTime? van de DatePicker.</summary>
public sealed class DateOnlyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            DateTime dt => DateOnly.FromDateTime(dt),
            _ when targetType == typeof(DateOnly) => Binding.DoNothing,
            _ => null,
        };
}

/// <summary>Afbeeldingsbytes → ImageSource; null bij geen of ongeldige afbeelding.</summary>
public sealed class BytesNaarAfbeeldingConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes)
            return null;
        try
        {
            var afbeelding = new BitmapImage();
            afbeelding.BeginInit();
            afbeelding.CacheOption = BitmapCacheOption.OnLoad;
            afbeelding.StreamSource = new MemoryStream(bytes);
            afbeelding.EndInit();
            afbeelding.Freeze();
            return afbeelding;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>"#RRGGBB" → SolidColorBrush; transparant bij een ongeldige kleur.</summary>
public sealed class KleurNaarBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is string tekst && ColorConverter.ConvertFromString(tekst) is Color kleur)
                return new SolidColorBrush(kleur);
        }
        catch (FormatException)
        {
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NietConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

public sealed class NullNaarZichtbaarheidConverter : IValueConverter
{
    /// <summary>Zichtbaar als de waarde niet null is; met parameter "omgekeerd" juist andersom.</summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var zichtbaar = value is not null;
        if (parameter is "omgekeerd")
            zichtbaar = !zichtbaar;
        return zichtbaar ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
