using CommunityToolkit.Mvvm.ComponentModel;
using Factures.Core.Domain;
using Factures.Presentation.ViewModels;

namespace Factures.Presentation.Services;

/// <summary>
/// Navigation sans framework : une page courante, et la vue suit par DataTemplate.
/// Les fabriques evitent une dependance circulaire entre navigation et pages.
/// </summary>
public sealed partial class NavigationService(
    Func<DashboardViewModel> dashboard,
    Func<InvoiceListViewModel> invoiceList,
    Func<InvoiceEditorViewModel> invoiceEditor,
    Func<ClientListViewModel> clientList,
    NotificationCenter notifications) : ObservableObject, INavigator
{
    [ObservableProperty]
    private PageViewModel? _currentPage;

    public Task ShowDashboardAsync() => OpenAsync(dashboard());

    public Task ShowInvoicesAsync(InvoiceStatus? status = null)
    {
        var page = invoiceList();
        page.Filters.SelectStatus(status);
        return OpenAsync(page);
    }

    public Task ShowClientsAsync() => OpenAsync(clientList());

    public Task EditInvoiceAsync(int? invoiceId)
    {
        var page = invoiceEditor();
        page.InvoiceId = invoiceId ?? 0;
        return OpenAsync(page);
    }

    private async Task OpenAsync(PageViewModel page)
    {
        notifications.Clear();
        CurrentPage = page;
        await page.LoadAsync();
    }
}
