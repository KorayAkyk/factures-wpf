using Factures.Core.Domain;

namespace Factures.Core.Abstractions;

public interface IInvoiceRepository
{
    Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Cree la facture si son Id vaut 0, sinon la remplace avec ses lignes. Renseigne l'Id.</summary>
    Task SaveAsync(Invoice invoice, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
