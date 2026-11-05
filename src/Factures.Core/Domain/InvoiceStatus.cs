namespace Factures.Core.Domain;

public enum InvoiceStatus
{
    Draft,
    Sent,
    Paid,
    Overdue,
}

public static class InvoiceStatusLabels
{
    public static string ToFrench(this InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "Brouillon",
        InvoiceStatus.Sent => "Envoyée",
        InvoiceStatus.Paid => "Payée",
        InvoiceStatus.Overdue => "En retard",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
