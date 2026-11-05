using Factures.Core.Domain;
using Factures.Data;
using Microsoft.Data.Sqlite;

namespace Factures.Tests.Data;

/// <summary>Tests sur un vrai fichier SQLite : c'est le SQL qui est verifie, pas un double.</summary>
public sealed class SqliteRepositoryTests : IAsyncLifetime
{
    private static readonly DateOnly Issue = new(2026, 3, 1);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "factures-tests-" + Guid.NewGuid().ToString("N"));
    private SqliteDatabase _database = null!;
    private SqliteClientRepository _clients = null!;
    private SqliteInvoiceRepository _invoices = null!;

    public async Task InitializeAsync()
    {
        _database = new SqliteDatabase(Path.Combine(_directory, "sous-dossier", "test.db"));
        await _database.MigrateAsync();
        _clients = new SqliteClientRepository(_database);
        _invoices = new SqliteInvoiceRepository(_database);
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_directory, recursive: true);
        return Task.CompletedTask;
    }

    private async Task<Client> AddClientAsync(string name = "Client")
    {
        var client = new Client(name, "a@b.example", 45);
        await _clients.SaveAsync(client);
        return client;
    }

    private async Task<Invoice> AddInvoiceAsync(Client client, params InvoiceLine[] lines)
    {
        var invoice = new Invoice(client.Id, "Objet", Issue, Issue.AddDays(30), lines);
        await _invoices.SaveAsync(invoice);
        return invoice;
    }

    [Fact]
    public async Task Migrating_twice_changes_nothing()
    {
        await AddClientAsync();

        await _database.MigrateAsync();

        await using var connection = await _database.OpenAsync();
        Assert.Equal(1, await SqliteDatabase.GetSchemaVersionAsync(connection));
        Assert.Single(await _clients.GetAllAsync());
    }

    [Fact]
    public async Task Client_round_trip_and_update()
    {
        var client = await AddClientAsync("Atelier d'Été");
        Assert.True(client.Id > 0);

        client.Update("Nouveau nom", "c@d.example", 10);
        await _clients.SaveAsync(client);

        var read = await _clients.GetByIdAsync(client.Id);
        Assert.Equal("Nouveau nom", read!.Name);
        Assert.Equal("c@d.example", read.Email);
        Assert.Equal(10, read.PaymentTermDays);
    }

    [Fact]
    public async Task Sql_injection_attempt_is_stored_as_plain_text()
    {
        var name = "x'); DROP TABLE clients; --";

        var client = await AddClientAsync(name);

        Assert.Equal(name, (await _clients.GetByIdAsync(client.Id))!.Name);
    }

    [Fact]
    public async Task Decimals_keep_their_exact_value()
    {
        var client = await AddClientAsync();
        var invoice = await AddInvoiceAsync(client, new InvoiceLine("Précis", 1.333m, 1234567.89m, 5.5m));

        var line = Assert.Single((await _invoices.GetByIdAsync(invoice.Id))!.Lines);

        Assert.Equal(1.333m, line.Quantity);
        Assert.Equal(1234567.89m, line.UnitPrice);
        Assert.Equal(5.5m, line.VatRate);
    }

    [Fact]
    public async Task Lines_come_back_in_their_order()
    {
        var client = await AddClientAsync();
        var invoice = await AddInvoiceAsync(
            client, new InvoiceLine("B", 1, 1, 0), new InvoiceLine("A", 1, 1, 0), new InvoiceLine("C", 1, 1, 0));

        var read = (await _invoices.GetAllAsync()).Single(other => other.Id == invoice.Id);

        Assert.Equal(["B", "A", "C"], read.Lines.Select(line => line.Description));
    }

    [Fact]
    public async Task Full_lifecycle_is_persisted()
    {
        var client = await AddClientAsync();
        var invoice = await AddInvoiceAsync(client, new InvoiceLine("L", 1, 100, 20));

        invoice.MarkSent("FAC-2026-0001", Issue);
        invoice.MarkPaid(Issue.AddDays(3));
        await _invoices.SaveAsync(invoice);

        var read = (await _invoices.GetByIdAsync(invoice.Id))!;
        Assert.Equal("FAC-2026-0001", read.Number);
        Assert.Equal(Issue, read.SentOn);
        Assert.Equal(Issue.AddDays(3), read.PaidOn);
        Assert.Equal(120m, read.TotalIncludingTax);
    }

    [Fact]
    public async Task Saving_a_draft_replaces_its_lines()
    {
        var client = await AddClientAsync();
        var invoice = await AddInvoiceAsync(client, new InvoiceLine("Ancienne", 1, 1, 0), new InvoiceLine("Ancienne 2", 1, 1, 0));

        invoice.UpdateDraft(client.Id, "Objet", Issue, Issue, [new InvoiceLine("Nouvelle", 1, 1, 0)]);
        await _invoices.SaveAsync(invoice);

        Assert.Equal("Nouvelle", Assert.Single((await _invoices.GetByIdAsync(invoice.Id))!.Lines).Description);
    }

    [Fact]
    public async Task Deleting_an_invoice_removes_its_lines()
    {
        var client = await AddClientAsync();
        var invoice = await AddInvoiceAsync(client, new InvoiceLine("L", 1, 1, 0));

        await _invoices.DeleteAsync(invoice.Id);

        Assert.Null(await _invoices.GetByIdAsync(invoice.Id));
        await using var connection = await _database.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM invoice_lines;";
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Database_refuses_to_delete_a_client_that_has_invoices()
    {
        var client = await AddClientAsync();
        await AddInvoiceAsync(client);

        await Assert.ThrowsAsync<SqliteException>(() => _clients.DeleteAsync(client.Id));
    }

    [Fact]
    public async Task Database_refuses_an_invoice_for_an_unknown_client()
    {
        var invoice = new Invoice(999, "Objet", Issue, Issue, []);

        await Assert.ThrowsAsync<SqliteException>(() => _invoices.SaveAsync(invoice));
        Assert.Empty(await _invoices.GetAllAsync());
    }

    [Fact]
    public async Task Database_refuses_a_duplicate_number_and_rolls_back_the_lines()
    {
        var client = await AddClientAsync();
        var first = await AddInvoiceAsync(client, new InvoiceLine("L", 1, 1, 0));
        first.MarkSent("FAC-2026-0001", Issue);
        await _invoices.SaveAsync(first);

        var second = await AddInvoiceAsync(client, new InvoiceLine("Conservee", 1, 1, 0));
        second.UpdateDraft(client.Id, "Objet", Issue, Issue, [new InvoiceLine("Perdue", 1, 1, 0)]);
        second.MarkSent("FAC-2026-0001", Issue);

        await Assert.ThrowsAsync<SqliteException>(() => _invoices.SaveAsync(second));

        var read = (await _invoices.GetByIdAsync(second.Id))!;
        Assert.Null(read.Number);
        Assert.Equal("Conservee", Assert.Single(read.Lines).Description);
    }
}
