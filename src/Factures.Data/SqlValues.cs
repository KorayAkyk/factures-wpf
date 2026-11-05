using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Factures.Data;

/// <summary>Conversions explicites entre types C# et colonnes texte, independantes de la culture.</summary>
internal static class SqlValues
{
    private const string DateFormat = "yyyy-MM-dd";

    public static string ToSql(this decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static string ToSql(this DateOnly value) => value.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static object ToSqlOrNull(this DateOnly? value) => value is { } date ? date.ToSql() : DBNull.Value;

    public static object ToSqlOrNull(this string? value) => value is null ? DBNull.Value : value;

    public static decimal GetDecimalText(this SqliteDataReader reader, string column) =>
        decimal.Parse(reader.GetString(reader.GetOrdinal(column)), NumberStyles.Number, CultureInfo.InvariantCulture);

    public static DateOnly GetDate(this SqliteDataReader reader, string column) =>
        DateOnly.ParseExact(reader.GetString(reader.GetOrdinal(column)), DateFormat, CultureInfo.InvariantCulture);

    public static DateOnly? GetNullableDate(this SqliteDataReader reader, string column) =>
        reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetDate(column);

    public static string? GetNullableString(this SqliteDataReader reader, string column) =>
        reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(reader.GetOrdinal(column));
}
