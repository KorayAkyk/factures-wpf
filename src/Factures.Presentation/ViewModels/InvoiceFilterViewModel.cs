using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Factures.Core.Domain;
using Factures.Core.Queries;

namespace Factures.Presentation.ViewModels;

public sealed record StatusOption(string Label, InvoiceStatus? Status);

public sealed record ClientOption(string Label, int? ClientId);

public sealed record SortOption(string Label, InvoiceSort Sort);

/// <summary>Criteres saisis a l'ecran. Chaque changement est signale pour relancer la requete LINQ.</summary>
public sealed partial class InvoiceFilterViewModel : ObservableObject
{
    private static readonly ClientOption AllClients = new("Tous les clients", null);

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private StatusOption _selectedStatus;

    [ObservableProperty]
    private ClientOption _selectedClient = AllClients;

    [ObservableProperty]
    private SortOption _selectedSort;

    [ObservableProperty]
    private DateOnly? _issuedFrom;

    [ObservableProperty]
    private DateOnly? _issuedTo;

    public InvoiceFilterViewModel()
    {
        _selectedStatus = StatusOptions[0];
        _selectedSort = SortOptions[0];
        ClientOptions.Add(AllClients);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<StatusOption> StatusOptions { get; } =
    [
        new("Tous les statuts", null),
        new(InvoiceStatus.Draft.ToFrench(), InvoiceStatus.Draft),
        new(InvoiceStatus.Sent.ToFrench(), InvoiceStatus.Sent),
        new(InvoiceStatus.Overdue.ToFrench(), InvoiceStatus.Overdue),
        new(InvoiceStatus.Paid.ToFrench(), InvoiceStatus.Paid),
    ];

    public IReadOnlyList<SortOption> SortOptions { get; } =
    [
        new("Plus récentes d'abord", InvoiceSort.IssueDateDescending),
        new("Échéance la plus proche", InvoiceSort.DueDateAscending),
        new("Montant décroissant", InvoiceSort.AmountDescending),
        new("Client (A à Z)", InvoiceSort.ClientAscending),
    ];

    public ObservableCollection<ClientOption> ClientOptions { get; } = [];

    public InvoiceFilter ToFilter() => new()
    {
        SearchText = SearchText,
        Status = SelectedStatus.Status,
        ClientId = SelectedClient.ClientId,
        IssuedFrom = IssuedFrom,
        IssuedTo = IssuedTo,
        Sort = SelectedSort.Sort,
    };

    public void SelectStatus(InvoiceStatus? status) =>
        SelectedStatus = StatusOptions.First(option => option.Status == status);

    /// <summary>Recharge la liste des clients en conservant la selection si elle existe encore.</summary>
    public void SetClients(IEnumerable<Client> clients)
    {
        var previous = SelectedClient.ClientId;
        ClientOptions.Clear();
        ClientOptions.Add(AllClients);
        foreach (var client in clients)
        {
            ClientOptions.Add(new ClientOption(client.Name, client.Id));
        }

        SelectedClient = ClientOptions.FirstOrDefault(option => option.ClientId == previous) ?? AllClients;
    }

    [RelayCommand]
    private void Reset()
    {
        SearchText = string.Empty;
        SelectedStatus = StatusOptions[0];
        SelectedClient = AllClients;
        SelectedSort = SortOptions[0];
        IssuedFrom = null;
        IssuedTo = null;
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
