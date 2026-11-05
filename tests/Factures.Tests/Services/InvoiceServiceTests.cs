using Factures.Core.Domain;
using Factures.Core.Services;
using Factures.Tests.Support;

namespace Factures.Tests.Services;

public class InvoiceServiceTests
{
    private readonly TestWorld _world = new();

    private async Task<Invoice> NewDraftAsync(Client client, int id = 0)
    {
        var line = new InvoiceLine("Ligne", 1, 100, 20);
        return await _world.Invoices.SaveDraftAsync(
            new InvoiceDraft(id, client.Id, "Objet", TestWorld.Today, TestWorld.Today.AddDays(30), [line]));
    }

    [Fact]
    public async Task Numbers_are_given_at_sending_and_follow_each_other()
    {
        var client = await _world.AddClientAsync("Client");
        var first = await NewDraftAsync(client);
        var second = await NewDraftAsync(client);

        Assert.Null(first.Number);

        // Le second brouillon part en premier : l'ordre d'envoi fixe le numero, pas la creation.
        Assert.Equal("FAC-2026-0001", await _world.Invoices.SendAsync(second.Id));
        Assert.Equal("FAC-2026-0002", await _world.Invoices.SendAsync(first.Id));
    }

    [Fact]
    public async Task Numbering_restarts_each_year()
    {
        var client = await _world.AddClientAsync("Client");
        await _world.Invoices.SendAsync((await NewDraftAsync(client)).Id);

        _world.Clock.Today = new DateOnly(2027, 1, 2);
        var next = await _world.Invoices.SendAsync((await NewDraftAsync(client)).Id);

        Assert.Equal("FAC-2027-0001", next);
    }

    [Theory]
    [InlineData(new string[0], "FAC-2026-0001")]
    [InlineData(new[] { "FAC-2026-0009", "FAC-2026-0002" }, "FAC-2026-0010")]
    [InlineData(new[] { "FAC-2025-0500" }, "FAC-2026-0001")]
    [InlineData(new[] { "AUTRE-12", "FAC-2026-abc" }, "FAC-2026-0001")]
    public void Next_number_ignores_other_years_and_foreign_formats(string[] existing, string expected)
    {
        Assert.Equal(expected, InvoiceNumberGenerator.Next(existing, 2026));
    }

    [Fact]
    public async Task Failed_sending_does_not_consume_a_number()
    {
        var client = await _world.AddClientAsync("Client");
        var empty = await _world.Invoices.SaveDraftAsync(
            new InvoiceDraft(0, client.Id, "Vide", TestWorld.Today, TestWorld.Today, []));

        await Assert.ThrowsAsync<BusinessRuleException>(() => _world.Invoices.SendAsync(empty.Id));

        Assert.Null(empty.Number);
        Assert.Equal("FAC-2026-0001", await _world.Invoices.SendAsync((await NewDraftAsync(client)).Id));
    }

    [Fact]
    public async Task Draft_must_belong_to_an_existing_client()
    {
        var draft = new InvoiceDraft(0, 404, "Objet", TestWorld.Today, TestWorld.Today, []);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _world.Invoices.SaveDraftAsync(draft));

        Assert.Equal("Choisissez un client.", error.Message);
    }

    [Fact]
    public async Task Saving_an_existing_draft_updates_it_instead_of_creating_another()
    {
        var client = await _world.AddClientAsync("Client");
        var draft = await NewDraftAsync(client);

        var updated = await _world.Invoices.SaveDraftAsync(new InvoiceDraft(
            draft.Id, client.Id, "Nouvel objet", TestWorld.Today, TestWorld.Today, [new InvoiceLine("L", 2, 10, 0)]));

        Assert.Same(draft, updated);
        Assert.Equal("Nouvel objet", updated.Subject);
        Assert.Equal(20m, updated.TotalIncludingTax);
        Assert.Single(await _world.InvoiceRepository.GetAllAsync());
    }

    [Fact]
    public async Task Payment_is_dated_today()
    {
        var client = await _world.AddClientAsync("Client");
        var invoice = await NewDraftAsync(client);
        await _world.Invoices.SendAsync(invoice.Id);

        await _world.Invoices.MarkPaidAsync(invoice.Id);

        Assert.Equal(TestWorld.Today, invoice.PaidOn);
    }

    [Fact]
    public async Task Only_drafts_can_be_deleted()
    {
        var client = await _world.AddClientAsync("Client");
        var draft = await NewDraftAsync(client);
        var sent = await _world.AddInvoiceAsync(client, InvoiceStatus.Sent);

        await _world.Invoices.DeleteDraftAsync(draft.Id);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _world.Invoices.DeleteDraftAsync(sent.Id));

        Assert.Contains("Seul un brouillon", error.Message);
        Assert.Equal(sent.Id, Assert.Single(await _world.InvoiceRepository.GetAllAsync()).Id);
    }

    [Fact]
    public async Task Missing_invoice_gives_a_readable_error()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _world.Invoices.SendAsync(404));

        Assert.Equal("Cette facture n'existe plus.", error.Message);
    }

    [Fact]
    public async Task Client_with_invoices_cannot_be_deleted()
    {
        var client = await _world.AddClientAsync("Client");
        await _world.AddInvoiceAsync(client, InvoiceStatus.Draft);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _world.Clients.DeleteAsync(client.Id));

        Assert.Contains("1 facture(s)", error.Message);
        Assert.NotNull(await _world.ClientRepository.GetByIdAsync(client.Id));
    }

    [Fact]
    public async Task Client_names_must_stay_unique_ignoring_case()
    {
        await _world.AddClientAsync("Atelier");

        await Assert.ThrowsAsync<BusinessRuleException>(() => _world.Clients.SaveAsync(0, " atelier ", string.Empty, 30));
    }

    [Fact]
    public async Task Client_can_keep_its_own_name_when_updated()
    {
        var client = await _world.AddClientAsync("Atelier");

        var saved = await _world.Clients.SaveAsync(client.Id, "Atelier", string.Empty, 45);

        Assert.Equal(45, saved.PaymentTermDays);
    }

    [Fact]
    public async Task Refused_duplicate_rename_leaves_the_client_unchanged()
    {
        await _world.AddClientAsync("Atelier");
        var other = await _world.AddClientAsync("Studio");

        await Assert.ThrowsAsync<BusinessRuleException>(() => _world.Clients.SaveAsync(other.Id, "Atelier", string.Empty, 30));

        Assert.Equal("Studio", other.Name);
    }
}
