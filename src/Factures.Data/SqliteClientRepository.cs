using Factures.Core.Abstractions;
using Factures.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Factures.Data;

public sealed class SqliteClientRepository(SqliteDatabase database) : IClientRepository
{
    private const string SelectColumns = "SELECT id, name, email, payment_term_days FROM clients";

    public async Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} ORDER BY id;";

        var clients = new List<Client>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            clients.Add(Read(reader));
        }

        return clients;
    }

    public async Task<Client?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectColumns} WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task SaveAsync(Client client, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = client.Id == 0
            ? """
              INSERT INTO clients (name, email, payment_term_days) VALUES ($name, $email, $term);
              SELECT last_insert_rowid();
              """
            : """
              UPDATE clients SET name = $name, email = $email, payment_term_days = $term WHERE id = $id;
              SELECT $id;
              """;
        command.Parameters.AddWithValue("$id", client.Id);
        command.Parameters.AddWithValue("$name", client.Name);
        command.Parameters.AddWithValue("$email", client.Email);
        command.Parameters.AddWithValue("$term", client.PaymentTermDays);

        var id = await command.ExecuteScalarAsync(cancellationToken);
        client.Id = Convert.ToInt32(id, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM clients WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Client Read(SqliteDataReader reader) =>
        new(reader.GetString(1), reader.GetString(2), reader.GetInt32(3)) { Id = reader.GetInt32(0) };
}
