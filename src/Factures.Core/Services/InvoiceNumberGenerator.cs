using System.Globalization;

namespace Factures.Core.Services;

/// <summary>Numerotation annuelle continue : FAC-2026-0001, FAC-2026-0002, etc.</summary>
public static class InvoiceNumberGenerator
{
    public static string Next(IEnumerable<string?> existingNumbers, int year)
    {
        var prefix = $"FAC-{year}-";
        var last = existingNumbers
            .Where(number => number is not null && number.StartsWith(prefix, StringComparison.Ordinal))
            .Select(number => int.TryParse(number![prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{last + 1:0000}";
    }
}
