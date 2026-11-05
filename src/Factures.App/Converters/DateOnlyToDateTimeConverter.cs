using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Factures.App.Converters;

/// <summary>Le DatePicker de WPF ne connait que DateTime ; les ViewModels parlent en DateOnly.</summary>
[ValueConversion(typeof(DateOnly), typeof(DateTime))]
public sealed class DateOnlyToDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : null;

    /// <summary>Un champ vide n'efface une date que si la propriete cible accepte null.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime)
        {
            return DateOnly.FromDateTime(dateTime);
        }

        return Nullable.GetUnderlyingType(targetType) is not null ? null : DependencyProperty.UnsetValue;
    }
}
