using Factures.Core.Domain;
using Factures.Core.Queries;
using Factures.Core.Reports;

namespace Factures.Tests.Reports;

public class DashboardCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    private static InvoiceRow Row(
        InvoiceStatus status, decimal total, string client = "Client", int daysOverdue = 0, string? paidOn = null) =>
        new(1, "N", 1, client, "Objet", Today, Today, paidOn is null ? null : DateOnly.Parse(paidOn), total, total, status, daysOverdue);

    [Fact]
    public void Totals_are_split_by_status()
    {
        InvoiceRow[] rows =
        [
            Row(InvoiceStatus.Draft, 10),
            Row(InvoiceStatus.Draft, 15),
            Row(InvoiceStatus.Sent, 100),
            Row(InvoiceStatus.Overdue, 200, daysOverdue: 5),
            Row(InvoiceStatus.Overdue, 300, daysOverdue: 40),
            Row(InvoiceStatus.Paid, 1000, paidOn: "2026-02-01"),
        ];

        var summary = DashboardCalculator.Compute(rows, Today);

        Assert.Equal(25m, summary.DraftAmount);
        Assert.Equal(2, summary.DraftCount);
        Assert.Equal(100m, summary.Outstanding);
        Assert.Equal(500m, summary.OverdueAmount);
        Assert.Equal(2, summary.OverdueCount);
        Assert.Equal(1000m, summary.PaidThisYear);
    }

    [Fact]
    public void Paid_this_year_excludes_previous_years()
    {
        InvoiceRow[] rows =
        [
            Row(InvoiceStatus.Paid, 100, paidOn: "2026-01-01"),
            Row(InvoiceStatus.Paid, 999, paidOn: "2025-12-31"),
        ];

        Assert.Equal(100m, DashboardCalculator.Compute(rows, Today).PaidThisYear);
    }

    [Fact]
    public void Overdue_invoices_are_listed_oldest_first()
    {
        InvoiceRow[] rows =
        [
            Row(InvoiceStatus.Overdue, 1, daysOverdue: 3),
            Row(InvoiceStatus.Overdue, 2, daysOverdue: 90),
            Row(InvoiceStatus.Overdue, 3, daysOverdue: 12),
        ];

        var summary = DashboardCalculator.Compute(rows, Today);

        Assert.Equal([90, 12, 3], summary.OverdueInvoices.Select(row => row.DaysOverdue));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(30, 0)]
    [InlineData(31, 1)]
    [InlineData(60, 1)]
    [InlineData(61, 2)]
    [InlineData(500, 2)]
    public void Aging_buckets_have_exact_bounds(int daysOverdue, int expectedBucket)
    {
        var summary = DashboardCalculator.Compute([Row(InvoiceStatus.Overdue, 42, daysOverdue: daysOverdue)], Today);

        for (var bucket = 0; bucket < summary.Aging.Count; bucket++)
        {
            Assert.Equal(bucket == expectedBucket ? 42m : 0m, summary.Aging[bucket].Amount);
        }
    }

    [Fact]
    public void Last_six_months_include_empty_months_and_cross_the_year()
    {
        var january = new DateOnly(2026, 1, 20);
        InvoiceRow[] rows =
        [
            Row(InvoiceStatus.Paid, 10, paidOn: "2026-01-05"),
            Row(InvoiceStatus.Paid, 20, paidOn: "2026-01-20"),
            Row(InvoiceStatus.Paid, 40, paidOn: "2025-08-31"),
            Row(InvoiceStatus.Paid, 80, paidOn: "2025-07-31"),
        ];

        var months = DashboardCalculator.Compute(rows, january).LastSixMonths;

        Assert.Equal(
            [(2025, 8, 40m), (2025, 9, 0m), (2025, 10, 0m), (2025, 11, 0m), (2025, 12, 0m), (2026, 1, 30m)],
            months.Select(month => (month.Year, month.Month, month.Amount)));
    }

    [Fact]
    public void Top_clients_are_ranked_by_amount_paid_and_limited_to_five()
    {
        var rows = new List<InvoiceRow> { Row(InvoiceStatus.Sent, 9999, client: "Pas encore paye") };
        rows.AddRange(Enumerable.Range(1, 6).Select(i => Row(InvoiceStatus.Paid, i * 100, client: $"Client {i}", paidOn: "2026-02-01")));
        rows.Add(Row(InvoiceStatus.Paid, 50, client: "Client 1", paidOn: "2026-03-01"));

        var top = DashboardCalculator.Compute(rows, Today).TopClients;

        Assert.Equal(5, top.Count);
        Assert.Equal("Client 6", top[0].ClientName);
        Assert.DoesNotContain(top, total => total.ClientName is "Pas encore paye" or "Client 1");
    }

    [Fact]
    public void Client_total_counts_each_paid_invoice()
    {
        InvoiceRow[] rows =
        [
            Row(InvoiceStatus.Paid, 100, client: "A", paidOn: "2026-02-01"),
            Row(InvoiceStatus.Paid, 50, client: "A", paidOn: "2026-03-01"),
        ];

        var total = Assert.Single(DashboardCalculator.Compute(rows, Today).TopClients);

        Assert.Equal(new ClientTotal("A", 2, 150m), total);
    }

    [Fact]
    public void No_invoice_gives_zero_everywhere_without_failing()
    {
        var summary = DashboardCalculator.Compute([], Today);

        Assert.Equal(0m, summary.PaidThisYear);
        Assert.Empty(summary.OverdueInvoices);
        Assert.Equal(6, summary.LastSixMonths.Count);
    }
}
