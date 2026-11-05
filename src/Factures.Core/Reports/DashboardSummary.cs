using Factures.Core.Queries;

namespace Factures.Core.Reports;

public sealed record MonthlyRevenue(int Year, int Month, decimal Amount);

public sealed record ClientTotal(string ClientName, int InvoiceCount, decimal Amount);

public sealed record AgingBucket(string Label, int InvoiceCount, decimal Amount);

public sealed record DashboardSummary
{
    /// <summary>Encaisse depuis le 1er janvier de l'annee en cours (TTC).</summary>
    public required decimal PaidThisYear { get; init; }

    /// <summary>Envoye, pas encore echu et pas encore paye (TTC).</summary>
    public required decimal Outstanding { get; init; }

    public required decimal OverdueAmount { get; init; }

    public required int OverdueCount { get; init; }

    public required decimal DraftAmount { get; init; }

    public required int DraftCount { get; init; }

    /// <summary>Factures en retard, de la plus ancienne a la plus recente.</summary>
    public required IReadOnlyList<InvoiceRow> OverdueInvoices { get; init; }

    public required IReadOnlyList<AgingBucket> Aging { get; init; }

    /// <summary>Encaissements des six derniers mois, mois vides compris, du plus ancien au plus recent.</summary>
    public required IReadOnlyList<MonthlyRevenue> LastSixMonths { get; init; }

    /// <summary>Les cinq clients qui ont le plus paye.</summary>
    public required IReadOnlyList<ClientTotal> TopClients { get; init; }
}
