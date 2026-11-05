using Factures.Core.Domain;
using Factures.Presentation.Services;
using Factures.Presentation.ViewModels;
using Factures.Tests.Support;

namespace Factures.Tests.ViewModels;

public class NavigationTests
{
    private readonly TestWorld _world = new();
    private readonly NavigationService _navigation;
    private readonly MainViewModel _main;

    public NavigationTests()
    {
        // Meme cablage que l'application : les pages recoivent le vrai service de navigation.
        NavigationService navigation = null!;
        navigation = new NavigationService(
            () => new DashboardViewModel(_world.Invoices, _world.Clock, navigation, _world.Notifications),
            () => new InvoiceListViewModel(_world.Invoices, _world.Clients, navigation, _world.Dialogs, _world.Files, _world.Notifications),
            () => new InvoiceEditorViewModel(_world.Invoices, _world.Clients, navigation, _world.Clock, _world.Notifications),
            () => new ClientListViewModel(_world.Clients, _world.Dialogs, _world.Notifications),
            _world.Notifications);
        _navigation = navigation;
        _main = new MainViewModel(navigation, _world.Notifications);
    }

    private AppSection[] ActiveSections() =>
        _main.NavItems.Where(item => item.IsActive).Select(item => item.Section).ToArray();

    [Fact]
    public async Task Application_opens_on_the_dashboard_with_its_menu_entry_active()
    {
        await _main.InitializeCommand.ExecuteAsync(null);

        Assert.IsType<DashboardViewModel>(_navigation.CurrentPage);
        Assert.Equal([AppSection.Dashboard], ActiveSections());
    }

    [Fact]
    public async Task Menu_entries_open_their_page()
    {
        await _main.NavItems.Single(item => item.Section == AppSection.Clients).OpenCommand.ExecuteAsync(null);
        Assert.IsType<ClientListViewModel>(_navigation.CurrentPage);

        await _main.NavItems.Single(item => item.Section == AppSection.Invoices).OpenCommand.ExecuteAsync(null);
        Assert.IsType<InvoiceListViewModel>(_navigation.CurrentPage);
        Assert.Equal([AppSection.Invoices], ActiveSections());
    }

    [Fact]
    public async Task Editor_keeps_the_invoices_menu_entry_active()
    {
        await _navigation.EditInvoiceAsync(null);

        Assert.IsType<InvoiceEditorViewModel>(_navigation.CurrentPage);
        Assert.Equal([AppSection.Invoices], ActiveSections());
    }

    [Fact]
    public async Task Status_passed_to_the_list_is_already_applied_once_loaded()
    {
        var client = await _world.AddClientAsync("Atelier");
        await _world.AddInvoiceAsync(client, InvoiceStatus.Draft);
        var overdue = await _world.AddInvoiceAsync(client, InvoiceStatus.Overdue);

        await _navigation.ShowInvoicesAsync(InvoiceStatus.Overdue);

        var page = Assert.IsType<InvoiceListViewModel>(_navigation.CurrentPage);
        Assert.Equal(overdue.Id, Assert.Single(page.Results).Id);
    }

    [Fact]
    public async Task Navigating_clears_the_previous_message()
    {
        _world.Notifications.ShowError("Ancienne erreur");

        await _navigation.ShowClientsAsync();

        Assert.False(_world.Notifications.HasMessage);
    }

    [Fact]
    public async Task Full_scenario_saved_draft_appears_in_the_list_with_its_message()
    {
        var client = await _world.AddClientAsync("Atelier");
        await _navigation.EditInvoiceAsync(null);
        var editor = Assert.IsType<InvoiceEditorViewModel>(_navigation.CurrentPage);
        editor.SelectedClient = client;
        editor.Subject = "Portail";
        editor.Lines[0].Description = "Conception";
        editor.Lines[0].UnitPrice = 10;

        await editor.SaveCommand.ExecuteAsync(null);

        var list = Assert.IsType<InvoiceListViewModel>(_navigation.CurrentPage);
        Assert.Equal("Portail", Assert.Single(list.Results).Subject);
        Assert.Equal("Brouillon enregistré.", _world.Notifications.Message);
    }

    [Fact]
    public void Information_does_not_hide_an_unread_error_but_clear_allows_it_again()
    {
        var notifications = new NotificationCenter();

        notifications.ShowError("Erreur");
        notifications.ShowInfo("Information");
        Assert.Equal("Erreur", notifications.Message);

        notifications.ClearCommand.Execute(null);
        Assert.False(notifications.HasMessage);

        notifications.ShowInfo("Information");
        Assert.Equal("Information", notifications.Message);
        Assert.False(notifications.IsError);
    }
}
