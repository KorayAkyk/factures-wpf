using Factures.Core.Abstractions;
using Factures.Core.Domain;

namespace Factures.Core.Services;

public sealed class ClientService(IClientRepository clients, IInvoiceRepository invoices)
{
    public async Task<IReadOnlyList<Client>> GetAllSortedAsync(CancellationToken cancellationToken = default)
    {
        var all = await clients.GetAllAsync(cancellationToken);
        return all.OrderBy(client => client.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<Client> SaveAsync(
        int id, string name, string email, int paymentTermDays, CancellationToken cancellationToken = default)
    {
        // Valide d'abord sur une copie : un refus ne laisse aucun objet a moitie modifie.
        var candidate = new Client(name, email, paymentTermDays);
        var all = await clients.GetAllAsync(cancellationToken);
        var isDuplicate = all.Any(other =>
            other.Id != id && string.Equals(other.Name, candidate.Name, StringComparison.CurrentCultureIgnoreCase));
        if (isDuplicate)
        {
            throw new BusinessRuleException($"Un client nommé « {candidate.Name} » existe déjà.");
        }

        var client = candidate;
        if (id != 0)
        {
            client = await clients.GetByIdAsync(id, cancellationToken)
                ?? throw new BusinessRuleException("Ce client n'existe plus.");
            client.Update(name, email, paymentTermDays);
        }

        await clients.SaveAsync(client, cancellationToken);
        return client;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var all = await invoices.GetAllAsync(cancellationToken);
        var invoiceCount = all.Count(invoice => invoice.ClientId == id);
        if (invoiceCount > 0)
        {
            throw new BusinessRuleException($"Ce client a {invoiceCount} facture(s) : il ne peut pas être supprimé.");
        }

        await clients.DeleteAsync(id, cancellationToken);
    }
}
