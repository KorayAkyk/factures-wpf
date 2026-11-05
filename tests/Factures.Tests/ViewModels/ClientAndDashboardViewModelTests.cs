using Factures.Core.Domain;
using Factures.Presentation.ViewModels;
using Factures.Tests.Support;

namespace Factures.Tests.ViewModels;

public class ClientAndDashboardViewModelTests
{
    private readonly TestWorld _world = new();

    private ClientListViewModel NewClientList() => new(_world.Clients, _world.Dialogs, _world.Notifications);

    private DashboardViewModel NewDashboard() => new(_world.Invoices, _world.Clock, _world.Navigator, _world.Notifications);

    [Fact]
    public async Task Selecting_a_client_fills_the_form_and_new_empties_it()
    {
        var client = await _world.AddClientAsync("Atelier", paymentTermDays: 45);
        var viewModel = NewClientList();
        await viewModel.LoadAsync();

        viewModel.SelectedClient = client;
        Assert.Equal("Atelier", viewModel.Name);
        Assert.Equal(45, viewModel.PaymentTermDays);
        Assert.Equal("Modifier le client", viewModel.FormTitle);

        viewModel.NewCommand.Execute(null);
        Assert.Equal(string.Empty, viewModel.Name);
        Assert.Equal(Client.DefaultPaymentTermDays, viewModel.PaymentTermDays);
        Assert.Equal("Nouveau client", viewModel.FormTitle);
    }

    [Fact]
    public async Task New_empties_a_form_typed_with_nothing_selected()
    {
        var viewModel = NewClientList();
        await viewModel.LoadAsync();
        viewModel.Name = "Texte en cours";

        viewModel.NewCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.Name);
    }

    [Fact]
    public async Task Saving_a_new_client_adds_and_selects_it()
    {
        var viewModel = NewClientList();
        await viewModel.LoadAsync();
        viewModel.Name = "Atelier";
        viewModel.Email = "a@b.example";

        await viewModel.SaveCommand.ExecuteAsync(null);

        var client = Assert.Single(viewModel.Clients);
        Assert.Equal(client, viewModel.SelectedClient);
        Assert.Contains("enregistré", _world.Notifications.Message);
    }

    [Fact]
    public async Task Saving_a_selected_client_updates_it_without_duplicating()
    {
        var client = await _world.AddClientAsync("Atelier");
        var viewModel = NewClientList();
        await viewModel.LoadAsync();
        viewModel.SelectedClient = client;
        viewModel.Name = "Atelier renommé";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Atelier renommé", Assert.Single(viewModel.Clients).Name);
    }

    [Fact]
    public async Task Invalid_client_keeps_the_form_and_shows_the_error()
    {
        var viewModel = NewClientList();
        await viewModel.LoadAsync();
        viewModel.Name = "Atelier";
        viewModel.Email = "pas-une-adresse";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(_world.Notifications.IsError);
        Assert.Contains("n'est pas valide", _world.Notifications.Message);
        Assert.Equal("Atelier", viewModel.Name);
        Assert.Empty(viewModel.Clients);
    }

    [Fact]
    public async Task Delete_needs_a_selection_and_a_confirmation()
    {
        var client = await _world.AddClientAsync("Atelier");
        var viewModel = NewClientList();
        await viewModel.LoadAsync();
        Assert.False(viewModel.DeleteCommand.CanExecute(null));

        viewModel.SelectedClient = client;
        _world.Dialogs.ConfirmResult = false;
        await viewModel.DeleteCommand.ExecuteAsync(null);
        Assert.Single(viewModel.Clients);

        _world.Dialogs.ConfirmResult = true;
        await viewModel.DeleteCommand.ExecuteAsync(null);
        Assert.Empty(viewModel.Clients);
    }

    [Fact]
    public async Task Deleting_a_client_with_invoices_shows_why_it_is_refused()
    {
        var client = await _world.AddClientAsync("Atelier");
        await _world.AddInvoiceAsync(client, InvoiceStatus.Sent);
        var viewModel = NewClientList();
        await viewModel.LoadAsync();
        viewModel.SelectedClient = client;

        await viewModel.DeleteCommand.ExecuteAsync(null);

        Assert.Contains("ne peut pas être supprimé", _world.Notifications.Message);
        Assert.Single(viewModel.Clients);
    }

    [Fact]
    public async Task Dashboard_computes_totals_and_scales_bars_to_the_best_month()
    {
        var client = await _world.AddClientAsync("Atelier");
        await _world.AddInvoiceAsync(client, InvoiceStatus.Paid, unitPrice: 100);
        await _world.AddInvoiceAsync(client, InvoiceStatus.Overdue, unitPrice: 50);
        var viewModel = NewDashboard();

        await viewModel.LoadAsync();

        Assert.Equal(120m, viewModel.Summary!.PaidThisYear);
        Assert.Equal(60m, viewModel.Summary.OverdueAmount);
        Assert.True(viewModel.HasOverdue);
        Assert.False(viewModel.HasNoOverdue);
        Assert.Equal(6, viewModel.Months.Count);
        Assert.Equal(100d, viewModel.Months[^1].Percent);
        Assert.Equal("juin 2026", viewModel.Months[^1].Label);
    }

    [Fact]
    public async Task Dashboard_with_no_invoice_does_not_divide_by_zero()
    {
        var viewModel = NewDashboard();

        await viewModel.LoadAsync();

        Assert.All(viewModel.Months, month => Assert.Equal(0d, month.Percent));
        Assert.True(viewModel.HasNoOverdue);
        Assert.Null(_world.Notifications.Message);
    }

    [Fact]
    public async Task Dashboard_links_to_the_overdue_filter()
    {
        await NewDashboard().ShowOverdueCommand.ExecuteAsync(null);

        Assert.Equal(["invoices:Overdue"], _world.Navigator.Calls);
    }
}
