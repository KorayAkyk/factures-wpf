using Factures.Core.Domain;

namespace Factures.Core.Queries;

public enum InvoiceSort
{
    IssueDateDescending,
    DueDateAscending,
    AmountDescending,
    ClientAscending,
}

/// <summary>Criteres de recherche ; une valeur nulle desactive le critere.</summary>
public sealed record InvoiceFilter
{
    public string? SearchText { get; init; }

    public InvoiceStatus? Status { get; init; }

    public int? ClientId { get; init; }

    public DateOnly? IssuedFrom { get; init; }

    public DateOnly? IssuedTo { get; init; }

    public InvoiceSort Sort { get; init; } = InvoiceSort.IssueDateDescending;
}
