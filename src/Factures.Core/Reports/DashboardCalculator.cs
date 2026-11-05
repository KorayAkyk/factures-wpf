using Factures.Core.Domain;
using Factures.Core.Queries;

namespace Factures.Core.Reports;

public static class DashboardCalculator
{
    private const int TopClientCount = 5;
    private const int MonthCount = 6;

    public static DashboardSummary Compute(IReadOnlyCollection<InvoiceRow> rows, DateOnly today)
    {
        var overdue = rows
            .Where(row => row.Status == InvoiceStatus.Overdue)
            .OrderByDescending(row => row.DaysOverdue)
            .ThenBy(row => row.Id)
            .ToList();
        var drafts = rows.Where(row => row.Status == InvoiceStatus.Draft).ToList();
        var paid = rows.Where(row => row.PaidOn is not null).ToList();

        return new DashboardSummary
        {
            PaidThisYear = paid.Where(row => row.PaidOn!.Value.Year == today.Year).Sum(row => row.TotalIncludingTax),
            Outstanding = rows.Where(row => row.Status == InvoiceStatus.Sent).Sum(row => row.TotalIncludingTax),
            OverdueAmount = overdue.Sum(row => row.TotalIncludingTax),
            OverdueCount = overdue.Count,
            DraftAmount = drafts.Sum(row => row.TotalIncludingTax),
            DraftCount = drafts.Count,
            OverdueInvoices = overdue,
            Aging = ComputeAging(overdue),
            LastSixMonths = ComputeLastMonths(paid, today),
            TopClients = ComputeTopClients(paid),
        };
    }

    private static List<AgingBucket> ComputeAging(IReadOnlyCollection<InvoiceRow> overdue)
    {
        (string Label, int Min, int Max)[] ranges =
        [
            ("1 à 30 jours", 1, 30),
            ("31 à 60 jours", 31, 60),
            ("Plus de 60 jours", 61, int.MaxValue),
        ];

        return ranges
            .Select(range =>
            {
                var inRange = overdue.Where(row => row.DaysOverdue >= range.Min && row.DaysOverdue <= range.Max).ToList();
                return new AgingBucket(range.Label, inRange.Count, inRange.Sum(row => row.TotalIncludingTax));
            })
            .ToList();
    }

    private static List<MonthlyRevenue> ComputeLastMonths(IEnumerable<InvoiceRow> paid, DateOnly today)
    {
        var amountsByMonth = paid
            .GroupBy(row => (row.PaidOn!.Value.Year, row.PaidOn!.Value.Month))
            .ToDictionary(group => group.Key, group => group.Sum(row => row.TotalIncludingTax));

        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        return Enumerable.Range(0, MonthCount)
            .Select(offset => firstOfMonth.AddMonths(offset - MonthCount + 1))
            .Select(month => new MonthlyRevenue(
                month.Year,
                month.Month,
                amountsByMonth.GetValueOrDefault((month.Year, month.Month))))
            .ToList();
    }

    private static List<ClientTotal> ComputeTopClients(IEnumerable<InvoiceRow> paid) =>
        paid
            .GroupBy(row => row.ClientName)
            .Select(group => new ClientTotal(group.Key, group.Count(), group.Sum(row => row.TotalIncludingTax)))
            .OrderByDescending(total => total.Amount)
            .ThenBy(total => total.ClientName, StringComparer.Ordinal)
            .Take(TopClientCount)
            .ToList();
}
