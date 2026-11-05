using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Factures.Presentation.Services;

namespace Factures.Presentation.ViewModels;

public sealed partial class NavItemViewModel(AppSection section, string label, string automationId, Func<Task> open) : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    public AppSection Section { get; } = section;

    public string Label { get; } = label;

    public string AutomationId { get; } = automationId;

    public IAsyncRelayCommand OpenCommand { get; } = new AsyncRelayCommand(open);
}

public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(NavigationService navigation, NotificationCenter notifications)
    {
        Navigation = navigation;
        Notifications = notifications;
        NavItems =
        [
            new(AppSection.Dashboard, "Tableau de bord", "nav-dashboard", () => navigation.ShowDashboardAsync()),
            new(AppSection.Invoices, "Factures", "nav-invoices", () => navigation.ShowInvoicesAsync()),
            new(AppSection.Clients, "Clients", "nav-clients", () => navigation.ShowClientsAsync()),
        ];
        navigation.PropertyChanged += OnNavigationChanged;
    }

    public NavigationService Navigation { get; }

    public NotificationCenter Notifications { get; }

    public IReadOnlyList<NavItemViewModel> NavItems { get; }

    [RelayCommand]
    private Task InitializeAsync() => Navigation.ShowDashboardAsync();

    private void OnNavigationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NavigationService.CurrentPage))
        {
            return;
        }

        foreach (var item in NavItems)
        {
            item.IsActive = item.Section == Navigation.CurrentPage?.Section;
        }
    }
}
