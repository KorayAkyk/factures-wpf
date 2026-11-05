using Factures.Core.Domain;
using Factures.Presentation.ViewModels;
using Factures.Tests.Support;

namespace Factures.Tests.ViewModels;

public class InvoiceEditorViewModelTests
{
    private readonly TestWorld _world = new();

    private InvoiceEditorViewModel NewViewModel(int invoiceId = 0) => new(
        _world.Invoices, _world.Clients, _world.Navigator, _world.Clock, _world.Notifications)
    {
        InvoiceId = invoiceId,
    };

    [Fact]
    public async Task New_invoice_starts_with_today_and_one_empty_line()
    {
        await _world.AddClientAsync("Client");
        var viewModel = NewViewModel();

        await viewModel.LoadAsync();

        Assert.Equal("Nouvelle facture", viewModel.Title);
        Assert.Equal(TestWorld.Today, viewModel.IssueDate);
        Assert.Single(viewModel.Lines);
        Assert.Single(viewModel.Clients);
    }

    [Fact]
    public async Task Due_date_follows_the_client_terms_and_the_issue_date()
    {
        var client = await _world.AddClientAsync("Client", paymentTermDays: 45);
        var viewModel = NewViewModel();
        await viewModel.LoadAsync();

        viewModel.SelectedClient = client;
        Assert.Equal(TestWorld.Today.AddDays(45), viewModel.DueDate);

        viewModel.IssueDate = TestWorld.Today.AddDays(1);
        Assert.Equal(TestWorld.Today.AddDays(46), viewModel.DueDate);
    }

    [Fact]
    public async Task User_can_override_the_suggested_due_date()
    {
        var client = await _world.AddClientAsync("Client");
        var viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.SelectedClient = client;

        viewModel.DueDate = TestWorld.Today.AddDays(5);

        Assert.Equal(TestWorld.Today.AddDays(5), viewModel.DueDate);
    }

    [Fact]
    public async Task Totals_follow_line_edits_and_notify_the_view()
    {
        var viewModel = NewViewModel();
        await viewModel.LoadAsync();
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        var line = viewModel.Lines[0];
        line.Quantity = 2;
        line.UnitPrice = 150;
        line.VatRate = 10;

        Assert.Equal(300m, viewModel.TotalExcludingTax);
        Assert.Equal(30m, viewModel.TotalTax);
        Assert.Equal(330m, viewModel.TotalIncludingTax);
        Assert.Contains(nameof(InvoiceEditorViewModel.TotalIncludingTax), changes);
    }

    [Fact]
    public async Task Removed_line_no_longer_affects_totals()
    {
        var viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.AddLineCommand.Execute(null);
        var removed = viewModel.Lines[1];
        removed.UnitPrice = 100;
        viewModel.RemoveLineCommand.Execute(removed);
        var changes = 0;
        viewModel.PropertyChanged += (_, _) => changes++;

        removed.UnitPrice = 999;

        Assert.Equal(0m, viewModel.TotalExcludingTax);
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task Save_creates_a_draft_and_returns_to_the_list()
    {
        var client = await _world.AddClientAsync("Client");
        var viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.SelectedClient = client;
        viewModel.Subject = "Portail";
        viewModel.Lines[0].Description = "Conception";
        viewModel.Lines[0].UnitPrice = 100;

        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(await _world.InvoiceRepository.GetAllAsync());
        Assert.Equal(viewModel.TotalIncludingTax, saved.TotalIncludingTax);
        Assert.Equal(InvoiceStatus.Draft, saved.GetStatus(TestWorld.Today));
        Assert.Equal(["invoices:"], _world.Navigator.Calls);
        Assert.Equal("Brouillon enregistré.", _world.Notifications.Message);
    }

    [Theory]
    [InlineData(false, "Portail", "Conception", 1, "Choisissez un client.")]
    [InlineData(true, " ", "Conception", 1, "L'objet de la facture est obligatoire.")]
    [InlineData(true, "Portail", " ", 1, "Chaque ligne doit avoir une description.")]
    [InlineData(true, "Portail", "Conception", 0, "La quantité de « Conception » doit être positive.")]
    public async Task Invalid_form_stays_open_with_a_visible_error(
        bool withClient, string subject, string description, int quantity, string expectedError)
    {
        var client = await _world.AddClientAsync("Client");
        var viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.SelectedClient = withClient ? client : null;
        viewModel.Subject = subject;
        viewModel.Lines[0].Description = description;
        viewModel.Lines[0].Quantity = quantity;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(expectedError, _world.Notifications.Message);
        Assert.True(_world.Notifications.IsError);
        Assert.Empty(_world.Navigator.Calls);
        Assert.Empty(await _world.InvoiceRepository.GetAllAsync());
    }

    [Fact]
    public async Task Invoice_without_line_is_refused()
    {
        var client = await _world.AddClientAsync("Client");
        var viewModel = NewViewModel();
        await viewModel.LoadAsync();
        viewModel.SelectedClient = client;
        viewModel.Subject = "Portail";
        viewModel.RemoveLineCommand.Execute(viewModel.Lines[0]);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Ajoutez au moins une ligne.", _world.Notifications.Message);
    }

    [Fact]
    public async Task Existing_draft_is_loaded_without_changing_its_due_date()
    {
        var client = await _world.AddClientAsync("Client", paymentTermDays: 10);
        var draft = await _world.AddInvoiceAsync(client, InvoiceStatus.Draft, "Catalogue");
        var viewModel = NewViewModel(draft.Id);

        await viewModel.LoadAsync();

        Assert.Equal("Modifier le brouillon", viewModel.Title);
        Assert.Equal("Catalogue", viewModel.Subject);
        Assert.Same(client, viewModel.SelectedClient);
        Assert.Equal(draft.DueDate, viewModel.DueDate);
        Assert.Equal(draft.TotalIncludingTax, viewModel.TotalIncludingTax);
    }

    [Fact]
    public async Task Sent_invoice_cannot_be_opened_for_editing()
    {
        var client = await _world.AddClientAsync("Client");
        var sent = await _world.AddInvoiceAsync(client, InvoiceStatus.Sent);
        var viewModel = NewViewModel(sent.Id);

        await viewModel.LoadAsync();

        Assert.Equal("Une facture envoyée ne peut plus être modifiée.", _world.Notifications.Message);
        Assert.Empty(viewModel.Lines);
    }

    [Fact]
    public async Task Cancel_returns_to_the_list_without_saving()
    {
        var viewModel = NewViewModel();
        await viewModel.LoadAsync();

        await viewModel.CancelCommand.ExecuteAsync(null);

        Assert.Equal(["invoices:"], _world.Navigator.Calls);
        Assert.Empty(await _world.InvoiceRepository.GetAllAsync());
    }
}
