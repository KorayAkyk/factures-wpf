using System.Globalization;
using System.Text;
using Factures.Core.Queries;

namespace Factures.Core.Export;

/// <summary>
/// Export CSV lisible par Excel en francais : separateur point-virgule, virgule decimale,
/// UTF-8 avec BOM. Les cellules de texte sont protegees contre l'injection de formules.
/// </summary>
public static class InvoiceCsvExporter
{
    private const char Separator = ';';
    private const string Header = "Numéro;Client;Objet;Date;Échéance;Statut;Total HT;Total TTC;Payée le";
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string ToCsv(IEnumerable<InvoiceRow> rows)
    {
        var builder = new StringBuilder();
        builder.Append(Header).Append("\r\n");
        foreach (var row in rows)
        {
            string[] cells =
            [
                Text(row.DisplayNumber),
                Text(row.ClientName),
                Text(row.Subject),
                Date(row.IssueDate),
                Date(row.DueDate),
                Text(row.StatusLabel),
                Amount(row.TotalExcludingTax),
                Amount(row.TotalIncludingTax),
                row.PaidOn is { } paidOn ? Date(paidOn) : string.Empty,
            ];
            builder.AppendJoin(Separator, cells).Append("\r\n");
        }

        return builder.ToString();
    }

    public static byte[] ToBytes(IEnumerable<InvoiceRow> rows)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(ToCsv(rows))];
    }

    private static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Amount(decimal amount) => amount.ToString("0.00", French);

    private static string Text(string value)
    {
        var safe = value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" + value : value;
        var mustQuote = safe.IndexOfAny([Separator, '"', '\r', '\n']) >= 0;
        return mustQuote ? $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : safe;
    }
}
