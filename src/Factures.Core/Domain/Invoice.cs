namespace Factures.Core.Domain;

/// <summary>
/// Facture et cycle de vie : brouillon, envoyee, payee. Le retard n'est pas stocke :
/// il se deduit de la date du jour, donc il ne peut jamais etre desynchronise.
/// </summary>
public sealed class Invoice
{
    private readonly List<InvoiceLine> _lines = [];

    public Invoice(int clientId, string subject, DateOnly issueDate, DateOnly dueDate, IEnumerable<InvoiceLine> lines)
    {
        UpdateDraft(clientId, subject, issueDate, dueDate, lines);
    }

    public int Id { get; set; }

    /// <summary>Numero definitif, attribue seulement a l'envoi.</summary>
    public string? Number { get; private set; }

    public int ClientId { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public DateOnly IssueDate { get; private set; }

    public DateOnly DueDate { get; private set; }

    public DateOnly? SentOn { get; private set; }

    public DateOnly? PaidOn { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public decimal TotalExcludingTax => _lines.Sum(line => line.TotalExcludingTax);

    public decimal TotalTax => _lines.Sum(line => line.Tax);

    public decimal TotalIncludingTax => TotalExcludingTax + TotalTax;

    public InvoiceStatus GetStatus(DateOnly today)
    {
        if (PaidOn is not null)
        {
            return InvoiceStatus.Paid;
        }

        if (SentOn is null)
        {
            return InvoiceStatus.Draft;
        }

        return today > DueDate ? InvoiceStatus.Overdue : InvoiceStatus.Sent;
    }

    public int GetDaysOverdue(DateOnly today) =>
        GetStatus(today) == InvoiceStatus.Overdue ? today.DayNumber - DueDate.DayNumber : 0;

    public void UpdateDraft(int clientId, string subject, DateOnly issueDate, DateOnly dueDate, IEnumerable<InvoiceLine> lines)
    {
        if (SentOn is not null)
        {
            throw new BusinessRuleException("Une facture envoyée ne peut plus être modifiée.");
        }

        if (clientId <= 0)
        {
            throw new BusinessRuleException("Choisissez un client.");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new BusinessRuleException("L'objet de la facture est obligatoire.");
        }

        if (dueDate < issueDate)
        {
            throw new BusinessRuleException("L'échéance ne peut pas précéder la date de la facture.");
        }

        ClientId = clientId;
        Subject = subject.Trim();
        IssueDate = issueDate;
        DueDate = dueDate;
        _lines.Clear();
        _lines.AddRange(lines);
    }

    public void MarkSent(string number, DateOnly sentOn)
    {
        if (SentOn is not null)
        {
            throw new BusinessRuleException("Cette facture a déjà été envoyée.");
        }

        if (_lines.Count == 0 || TotalIncludingTax <= 0)
        {
            throw new BusinessRuleException("Une facture sans montant ne peut pas être envoyée.");
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            throw new BusinessRuleException("Le numéro de facture est obligatoire.");
        }

        Number = number;
        SentOn = sentOn;
    }

    public void MarkPaid(DateOnly paidOn)
    {
        if (SentOn is null)
        {
            throw new BusinessRuleException("Un brouillon ne peut pas être marqué comme payé : envoyez-le d'abord.");
        }

        if (PaidOn is not null)
        {
            throw new BusinessRuleException("Cette facture est déjà payée.");
        }

        if (paidOn < SentOn)
        {
            throw new BusinessRuleException("Le paiement ne peut pas précéder l'envoi de la facture.");
        }

        PaidOn = paidOn;
    }

    /// <summary>Reconstruit une facture depuis le stockage, sans repasser par les transitions.</summary>
    public static Invoice Restore(
        int id, string? number, int clientId, string subject, DateOnly issueDate, DateOnly dueDate,
        DateOnly? sentOn, DateOnly? paidOn, IEnumerable<InvoiceLine> lines) =>
        new(clientId, subject, issueDate, dueDate, lines)
        {
            Id = id,
            Number = number,
            SentOn = sentOn,
            PaidOn = paidOn,
        };
}
