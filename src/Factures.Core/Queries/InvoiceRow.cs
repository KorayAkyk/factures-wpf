using Factures.Core.Domain;

namespace Factures.Core.Queries;

/// <summary>Vue a plat d'une facture a une date donnee : ce que listes et rapports consomment.</summary>
public sealed record InvoiceRow(
    int Id,
    string? Number,
    int ClientId,
    string ClientName,
    string Subject,
    DateOnly IssueDate,
    DateOnly DueDate,
    DateOnly? PaidOn,
    decimal TotalExcludingTax,
    decimal TotalIncludingTax,
    InvoiceStatus Status,
    int DaysOverdue)
{
    public string DisplayNumber => Number ?? "(brouillon)";

    public string StatusLabel => Status.ToFrench();
}
