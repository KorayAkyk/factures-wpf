using Factures.Core.Abstractions;
using Factures.Core.Domain;
using Factures.Core.Queries;

namespace Factures.Core.Services;

public sealed record InvoiceDraft(
    int Id,
    int ClientId,
    string Subject,
    DateOnly IssueDate,
    DateOnly DueDate,
    IReadOnlyList<InvoiceLine> Lines);

/// <summary>Cas d'usage sur les factures : c'est ici que le domaine rencontre le stockage.</summary>
public sealed class InvoiceService(IInvoiceRepository invoices, IClientRepository clients, IClock clock)
{
    public async Task<IReadOnlyList<InvoiceRow>> GetRowsAsync(CancellationToken cancellationToken = default)
    {
        var allInvoices = await invoices.GetAllAsync(cancellationToken);
        var allClients = await clients.GetAllAsync(cancellationToken);
        return allInvoices.ToRows(allClients, clock.Today).ToList();
    }

    public Task<Invoice?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        invoices.GetByIdAsync(id, cancellationToken);

    public async Task<Invoice> SaveDraftAsync(InvoiceDraft draft, CancellationToken cancellationToken = default)
    {
        if (await clients.GetByIdAsync(draft.ClientId, cancellationToken) is null)
        {
            throw new BusinessRuleException("Choisissez un client.");
        }

        Invoice invoice;
        if (draft.Id == 0)
        {
            invoice = new Invoice(draft.ClientId, draft.Subject, draft.IssueDate, draft.DueDate, draft.Lines);
        }
        else
        {
            invoice = await RequireAsync(draft.Id, cancellationToken);
            invoice.UpdateDraft(draft.ClientId, draft.Subject, draft.IssueDate, draft.DueDate, draft.Lines);
        }

        await invoices.SaveAsync(invoice, cancellationToken);
        return invoice;
    }

    /// <summary>Envoie un brouillon : le numero definitif est attribue a ce moment-la.</summary>
    public async Task<string> SendAsync(int id, CancellationToken cancellationToken = default)
    {
        var invoice = await RequireAsync(id, cancellationToken);
        var today = clock.Today;
        var all = await invoices.GetAllAsync(cancellationToken);
        var number = InvoiceNumberGenerator.Next(all.Select(other => other.Number), today.Year);
        invoice.MarkSent(number, today);
        await invoices.SaveAsync(invoice, cancellationToken);
        return number;
    }

    public async Task MarkPaidAsync(int id, CancellationToken cancellationToken = default)
    {
        var invoice = await RequireAsync(id, cancellationToken);
        invoice.MarkPaid(clock.Today);
        await invoices.SaveAsync(invoice, cancellationToken);
    }

    public async Task DeleteDraftAsync(int id, CancellationToken cancellationToken = default)
    {
        var invoice = await RequireAsync(id, cancellationToken);
        if (invoice.GetStatus(clock.Today) != InvoiceStatus.Draft)
        {
            throw new BusinessRuleException("Seul un brouillon peut être supprimé : une facture envoyée reste dans la comptabilité.");
        }

        await invoices.DeleteAsync(id, cancellationToken);
    }

    private async Task<Invoice> RequireAsync(int id, CancellationToken cancellationToken) =>
        await invoices.GetByIdAsync(id, cancellationToken)
        ?? throw new BusinessRuleException("Cette facture n'existe plus.");
}
