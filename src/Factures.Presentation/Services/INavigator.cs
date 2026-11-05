using Factures.Core.Domain;

namespace Factures.Presentation.Services;

public interface INavigator
{
    Task ShowDashboardAsync();

    /// <summary>Ouvre la liste des factures, eventuellement deja filtree sur un statut.</summary>
    Task ShowInvoicesAsync(InvoiceStatus? status = null);

    Task ShowClientsAsync();

    /// <summary>Ouvre l'editeur : une nouvelle facture si l'identifiant est nul.</summary>
    Task EditInvoiceAsync(int? invoiceId);
}
