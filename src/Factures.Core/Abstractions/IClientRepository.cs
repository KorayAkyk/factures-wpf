using Factures.Core.Domain;

namespace Factures.Core.Abstractions;

public interface IClientRepository
{
    Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Client?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Cree le client si son Id vaut 0, sinon le met a jour. Renseigne l'Id.</summary>
    Task SaveAsync(Client client, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
