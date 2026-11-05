using System.Globalization;
using Factures.Core.Domain;

namespace Factures.Core.Queries;

public static class InvoiceQueries
{
    private const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>Joint factures et clients, et calcule le statut a la date donnee.</summary>
    public static IEnumerable<InvoiceRow> ToRows(
        this IEnumerable<Invoice> invoices, IEnumerable<Client> clients, DateOnly today) =>
        from invoice in invoices
        join client in clients on invoice.ClientId equals client.Id
        select new InvoiceRow(
            invoice.Id,
            invoice.Number,
            client.Id,
            client.Name,
            invoice.Subject,
            invoice.IssueDate,
            invoice.DueDate,
            invoice.PaidOn,
            invoice.TotalExcludingTax,
            invoice.TotalIncludingTax,
            invoice.GetStatus(today),
            invoice.GetDaysOverdue(today));

    /// <summary>Applique les criteres, puis le tri. L'ordre reste stable a egalite grace au numero.</summary>
    public static IEnumerable<InvoiceRow> Apply(this IEnumerable<InvoiceRow> rows, InvoiceFilter filter)
    {
        var terms = SplitTerms(filter.SearchText);

        var matching = rows
            .Where(row => filter.Status is null || row.Status == filter.Status)
            .Where(row => filter.ClientId is null || row.ClientId == filter.ClientId)
            .Where(row => filter.IssuedFrom is null || row.IssueDate >= filter.IssuedFrom)
            .Where(row => filter.IssuedTo is null || row.IssueDate <= filter.IssuedTo)
            .Where(row => terms.All(term => MatchesTerm(row, term)));

        return filter.Sort switch
        {
            InvoiceSort.DueDateAscending => matching.OrderBy(row => row.DueDate).ThenBy(row => row.Id),
            InvoiceSort.AmountDescending => matching.OrderByDescending(row => row.TotalIncludingTax).ThenBy(row => row.Id),
            InvoiceSort.ClientAscending => matching.OrderBy(row => row.ClientName, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.Id),
            _ => matching.OrderByDescending(row => row.IssueDate).ThenByDescending(row => row.Id),
        };
    }

    private static string[] SplitTerms(string? searchText) =>
        (searchText ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Recherche sans tenir compte de la casse ni des accents : « ecole » trouve « École ».</summary>
    private static bool MatchesTerm(InvoiceRow row, string term) =>
        new[] { row.Number, row.ClientName, row.Subject }
            .Any(field => field is not null && CultureInfo.InvariantCulture.CompareInfo.IndexOf(field, term, Loose) >= 0);
}
