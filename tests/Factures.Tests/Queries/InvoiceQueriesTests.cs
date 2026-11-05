using Factures.Core.Domain;
using Factures.Core.Queries;

namespace Factures.Tests.Queries;

public class InvoiceQueriesTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private static InvoiceRow Row(
        int id, string client = "Client", string subject = "Objet", string? number = null,
        InvoiceStatus status = InvoiceStatus.Sent, int clientId = 1, decimal total = 100, string issued = "2026-01-10", string? due = null) =>
        new(id, number, clientId, client, subject, DateOnly.Parse(issued), DateOnly.Parse(due ?? issued), null, total, total, status, 0);

    private static int[] Ids(IEnumerable<InvoiceRow> rows) => rows.Select(row => row.Id).ToArray();

    [Theory]
    [InlineData("ecole")]
    [InlineData("ÉCOLE")]
    [InlineData("École")]
    public void Search_ignores_case_and_accents(string term)
    {
        var rows = new[] { Row(1, client: "École de voile"), Row(2, client: "Boulangerie") };

        Assert.Equal([1], Ids(rows.Apply(new InvoiceFilter { SearchText = term })));
    }

    [Fact]
    public void Search_looks_in_number_client_and_subject()
    {
        var rows = new[]
        {
            Row(1, number: "FAC-2026-0042"),
            Row(2, client: "Client 0042"),
            Row(3, subject: "Lot 0042"),
            Row(4),
        };

        var result = Ids(rows.Apply(new InvoiceFilter { SearchText = "0042" }));

        Assert.Equal([1, 2, 3], result.Order());
    }

    [Fact]
    public void Every_search_term_must_match_somewhere()
    {
        var rows = new[]
        {
            Row(1, client: "Atelier Lumen", subject: "Catalogue"),
            Row(2, client: "Atelier Orme", subject: "Audit"),
            Row(3, client: "Studio", subject: "Catalogue"),
        };

        Assert.Equal([1], Ids(rows.Apply(new InvoiceFilter { SearchText = "  atelier   catalogue " })));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void Empty_search_keeps_everything(string? term)
    {
        var rows = new[] { Row(1), Row(2) };

        Assert.Equal(2, rows.Apply(new InvoiceFilter { SearchText = term }).Count());
    }

    [Fact]
    public void Status_and_client_filters_combine()
    {
        var rows = new[]
        {
            Row(1, status: InvoiceStatus.Overdue, clientId: 1),
            Row(2, status: InvoiceStatus.Overdue, clientId: 2),
            Row(3, status: InvoiceStatus.Paid, clientId: 1),
        };

        var filter = new InvoiceFilter { Status = InvoiceStatus.Overdue, ClientId = 1 };

        Assert.Equal([1], Ids(rows.Apply(filter)));
    }

    [Fact]
    public void Date_range_includes_both_bounds()
    {
        var rows = new[]
        {
            Row(1, issued: "2026-01-31"),
            Row(2, issued: "2026-02-01"),
            Row(3, issued: "2026-02-28"),
            Row(4, issued: "2026-03-01"),
        };

        var filter = new InvoiceFilter
        {
            IssuedFrom = new DateOnly(2026, 2, 1),
            IssuedTo = new DateOnly(2026, 2, 28),
            Sort = InvoiceSort.DueDateAscending,
        };

        Assert.Equal([2, 3], Ids(rows.Apply(filter)));
    }

    [Fact]
    public void Default_sort_shows_the_most_recent_first_with_stable_ties()
    {
        var rows = new[] { Row(1, issued: "2026-01-01"), Row(2, issued: "2026-03-01"), Row(3, issued: "2026-03-01") };

        Assert.Equal([3, 2, 1], Ids(rows.Apply(new InvoiceFilter())));
    }

    [Fact]
    public void Sort_by_amount_and_by_client()
    {
        var rows = new[] { Row(1, client: "Zeta", total: 50), Row(2, client: "Alpha", total: 10), Row(3, client: "Mu", total: 90) };

        Assert.Equal([3, 1, 2], Ids(rows.Apply(new InvoiceFilter { Sort = InvoiceSort.AmountDescending })));
        Assert.Equal([2, 3, 1], Ids(rows.Apply(new InvoiceFilter { Sort = InvoiceSort.ClientAscending })));
    }

    [Fact]
    public void ToRows_joins_clients_and_computes_status_at_the_given_date()
    {
        var client = new Client("Atelier", string.Empty) { Id = 5 };
        var invoice = new Invoice(5, "Objet", Today.AddDays(-40), Today.AddDays(-10), [new InvoiceLine("L", 1, 100, 20)]) { Id = 9 };
        invoice.MarkSent("FAC-9", Today.AddDays(-40));

        var row = Assert.Single(new[] { invoice }.ToRows([client], Today));

        Assert.Equal("Atelier", row.ClientName);
        Assert.Equal(InvoiceStatus.Overdue, row.Status);
        Assert.Equal(10, row.DaysOverdue);
        Assert.Equal(120m, row.TotalIncludingTax);
        Assert.Equal("En retard", row.StatusLabel);
    }

    [Fact]
    public void Draft_row_displays_a_placeholder_instead_of_a_number()
    {
        Assert.Equal("(brouillon)", Row(1, status: InvoiceStatus.Draft).DisplayNumber);
    }
}
