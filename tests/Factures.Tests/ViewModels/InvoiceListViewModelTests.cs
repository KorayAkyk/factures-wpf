using System.Text;
using Factures.Core.Domain;
using Factures.Presentation.ViewModels;
using Factures.Tests.Support;

namespace Factures.Tests.ViewModels;

public class InvoiceListViewModelTests
{
    private readonly TestWorld _world = new();

    private InvoiceListViewModel NewViewModel() => new(
        _world.Invoices, _world.Clients, _world.Navigator, _world.Dialogs, _world.Files, _world.Notifications);

    private async Task<(InvoiceListViewModel ViewModel, Invoice Draft, Invoice Sent, Invoice Overdue, Invoice Paid)> LoadedAsync()
    {
        var atelier = await _world.AddClientAsync("Atelier");
        var studio = await _world.AddClientAsync("Studio");
        var draft = await _world.AddInvoiceAsync(atelier, InvoiceStatus.Draft, "Catalogue");
        var sent = await _world.AddInvoiceAsync(atelier, InvoiceStatus.Sent, "Portail");
        var overdue = await _world.AddInvoiceAsync(studio, InvoiceStatus.Overdue, "Audit");
        var paid = await _world.AddInvoiceAsync(studio, InvoiceStatus.Paid, "Formation");
        var viewModel = NewViewModel();
        await viewModel.LoadAsync();
        return (viewModel, draft, sent, overdue, paid);
    }

    [Fact]
    public async Task Load_shows_every_invoice_and_a_summary()
    {
        var (viewModel, _, _, _, _) = await LoadedAsync();

        Assert.Equal(4, viewModel.Results.Count);
        Assert.StartsWith("4 facture(s), 480,00", viewModel.ResultSummary);
        Assert.Equal(["Tous les clients", "Atelier", "Studio"], viewModel.Filters.ClientOptions.Select(option => option.Label));
    }

    [Fact]
    public async Task Changing_a_filter_updates_results_immediately()
    {
        var (viewModel, _, _, overdue, _) = await LoadedAsync();

        viewModel.Filters.SelectStatus(InvoiceStatus.Overdue);

        Assert.Equal(overdue.Id, Assert.Single(viewModel.Results).Id);

        viewModel.Filters.ResetCommand.Execute(null);

        Assert.Equal(4, viewModel.Results.Count);
    }

    [Fact]
    public async Task Search_and_client_filters_combine()
    {
        var (viewModel, draft, _, _, _) = await LoadedAsync();

        viewModel.Filters.SearchText = "catal";
        viewModel.Filters.SelectedClient = viewModel.Filters.ClientOptions[1];
        Assert.Equal(draft.Id, Assert.Single(viewModel.Results).Id);

        viewModel.Filters.SelectedClient = viewModel.Filters.ClientOptions[2];
        Assert.Empty(viewModel.Results);
        Assert.False(viewModel.ExportCsvCommand.CanExecute(null));
    }

    [Fact]
    public async Task Null_pushed_by_a_combo_box_does_not_break_filtering()
    {
        var (viewModel, _, _, _, _) = await LoadedAsync();

        viewModel.Filters.SelectedClient = null;
        viewModel.Filters.SelectedStatus = null;
        viewModel.Filters.SelectedSort = null;

        Assert.Equal(4, viewModel.Results.Count);
    }

    [Fact]
    public async Task Commands_follow_the_selected_status()
    {
        var (viewModel, draft, sent, overdue, paid) = await LoadedAsync();

        void Select(Invoice invoice) => viewModel.SelectedInvoice = viewModel.Results.Single(row => row.Id == invoice.Id);
        bool[] State() =>
        [
            viewModel.EditCommand.CanExecute(null),
            viewModel.SendCommand.CanExecute(null),
            viewModel.MarkPaidCommand.CanExecute(null),
            viewModel.DeleteCommand.CanExecute(null),
        ];

        Assert.Equal([false, false, false, false], State());
        Select(draft);
        Assert.Equal([true, true, false, true], State());
        Select(sent);
        Assert.Equal([false, false, true, false], State());
        Select(overdue);
        Assert.Equal([false, false, true, false], State());
        Select(paid);
        Assert.Equal([false, false, false, false], State());
    }

    [Fact]
    public async Task Sending_asks_confirmation_then_refreshes_the_list()
    {
        var (viewModel, draft, _, _, _) = await LoadedAsync();
        viewModel.SelectedInvoice = viewModel.Results.Single(row => row.Id == draft.Id);

        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Single(_world.Dialogs.Confirmations);
        var row = viewModel.Results.Single(other => other.Id == draft.Id);
        Assert.Equal(InvoiceStatus.Sent, row.Status);
        Assert.Equal(row, viewModel.SelectedInvoice);
        Assert.Contains("marquée comme envoyée", _world.Notifications.Message);
        Assert.False(_world.Notifications.IsError);
    }

    [Fact]
    public async Task Refused_confirmation_changes_nothing()
    {
        var (viewModel, draft, _, _, _) = await LoadedAsync();
        viewModel.SelectedInvoice = viewModel.Results.Single(row => row.Id == draft.Id);
        _world.Dialogs.ConfirmResult = false;

        await viewModel.DeleteCommand.ExecuteAsync(null);
        await viewModel.SendCommand.ExecuteAsync(null);

        Assert.Equal(InvoiceStatus.Draft, viewModel.Results.Single(row => row.Id == draft.Id).Status);
        Assert.Null(_world.Notifications.Message);
    }

    [Fact]
    public async Task Deleting_a_draft_removes_it_and_clears_the_selection()
    {
        var (viewModel, draft, _, _, _) = await LoadedAsync();
        viewModel.SelectedInvoice = viewModel.Results.Single(row => row.Id == draft.Id);

        await viewModel.DeleteCommand.ExecuteAsync(null);

        Assert.DoesNotContain(viewModel.Results, row => row.Id == draft.Id);
        Assert.Null(viewModel.SelectedInvoice);
    }

    [Fact]
    public async Task Export_writes_only_the_filtered_rows()
    {
        var (viewModel, _, _, _, _) = await LoadedAsync();
        viewModel.Filters.SearchText = "Audit";
        _world.Dialogs.SavePath = "sortie.csv";

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        var content = Encoding.UTF8.GetString(_world.Files.Written["sortie.csv"]);
        Assert.Contains("Audit", content);
        Assert.DoesNotContain("Catalogue", content);
        Assert.Contains("1 facture(s) exportée(s)", _world.Notifications.Message);
    }

    [Fact]
    public async Task Cancelling_the_save_dialog_writes_nothing()
    {
        var (viewModel, _, _, _, _) = await LoadedAsync();
        _world.Dialogs.SavePath = null;

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.Empty(_world.Files.Written);
    }

    [Fact]
    public async Task Write_failure_is_shown_to_the_user()
    {
        var (viewModel, _, _, _, _) = await LoadedAsync();
        _world.Dialogs.SavePath = "sortie.csv";
        _world.Files.Failure = new IOException("Fichier ouvert dans Excel");

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.True(_world.Notifications.IsError);
        Assert.Contains("Fichier ouvert dans Excel", _world.Notifications.Message);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Load_failure_is_shown_to_the_user_instead_of_crashing()
    {
        _world.InvoiceRepository.FailNextRead = new InvalidOperationException("Base inaccessible");
        var viewModel = NewViewModel();

        await viewModel.LoadAsync();

        Assert.True(_world.Notifications.IsError);
        Assert.Contains("Base inaccessible", _world.Notifications.Message);
        Assert.Empty(viewModel.Results);
    }

    [Fact]
    public async Task New_and_edit_navigate_to_the_editor()
    {
        var (viewModel, draft, _, _, _) = await LoadedAsync();
        viewModel.SelectedInvoice = viewModel.Results.Single(row => row.Id == draft.Id);

        await viewModel.NewCommand.ExecuteAsync(null);
        await viewModel.EditCommand.ExecuteAsync(null);

        Assert.Equal(["edit:", $"edit:{draft.Id}"], _world.Navigator.Calls);
    }
}
