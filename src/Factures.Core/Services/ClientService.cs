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
        Client client;
        if (id == 0)
        {
            client = new Client(name, email, paymentTermDays);
        }
        else
        {
            client = await clients.GetByIdAsync(id, cancellationToken)
                ?? throw new BusinessRuleException("Ce client n'existe plus.");
            client.Update(name, email, paymentTermDays);
        }

        var all = await clients.GetAllAsync(cancellationToken);
        var isDuplicate = all.Any(other =>
            other.Id != client.Id && string.Equals(other.Name, client.Name, StringComparison.CurrentCultureIgnoreCase));
        if (isDuplicate)
        {
            throw new BusinessRuleException($"Un client nommé « {client.Name} » existe déjà.");
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
