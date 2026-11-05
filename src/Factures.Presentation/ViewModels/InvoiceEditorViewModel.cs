using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Factures.Core.Abstractions;
using Factures.Core.Domain;
using Factures.Core.Services;
using Factures.Presentation.Services;

namespace Factures.Presentation.ViewModels;

public sealed partial class InvoiceEditorViewModel : PageViewModel
{
    private readonly InvoiceService _invoices;
    private readonly ClientService _clients;
    private readonly INavigator _navigator;
    private readonly IClock _clock;
    private bool _isLoading;

    [ObservableProperty]
    private Client? _selectedClient;

    [ObservableProperty]
    private string _subject = string.Empty;

    [ObservableProperty]
    private DateOnly _issueDate;

    [ObservableProperty]
    private DateOnly _dueDate;

    public InvoiceEditorViewModel(
        InvoiceService invoices,
        ClientService clients,
        INavigator navigator,
        IClock clock,
        NotificationCenter notifications)
        : base(notifications, AppSection.Invoices, "Nouvelle facture")
    {
        _invoices = invoices;
        _clients = clients;
        _navigator = navigator;
        _clock = clock;
        _issueDate = clock.Today;
        _dueDate = clock.Today;
        Lines.CollectionChanged += OnLinesChanged;
    }

    /// <summary>Facture a modifier ; 0 pour une nouvelle facture.</summary>
    public int InvoiceId { get; set; }

    public ObservableCollection<Client> Clients { get; } = [];

    public ObservableCollection<InvoiceLineViewModel> Lines { get; } = [];

    public IReadOnlyList<decimal> VatRates { get; } = [0m, 5.5m, 10m, 20m];

    public decimal TotalExcludingTax => Lines.Sum(line => line.TotalExcludingTax);

    public decimal TotalTax => Lines.Sum(line => line.Tax);

    public decimal TotalIncludingTax => TotalExcludingTax + TotalTax;

    public override Task LoadAsync() => RunAsync(async () =>
    {
        _isLoading = true;
        try
        {
            Clients.Clear();
            foreach (var client in await _clients.GetAllSortedAsync())
            {
                Clients.Add(client);
            }

            if (InvoiceId == 0)
            {
                Lines.Add(new InvoiceLineViewModel());
            }
            else
            {
                await LoadExistingAsync();
            }
        }
        finally
        {
            _isLoading = false;
        }
    });

    [RelayCommand]
    private void AddLine() => Lines.Add(new InvoiceLineViewModel());

    [RelayCommand]
    private void RemoveLine(InvoiceLineViewModel? line)
    {
        if (line is not null)
        {
            Lines.Remove(line);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var saved = await RunAsync(async () =>
        {
            if (SelectedClient is null)
            {
                throw new BusinessRuleException("Choisissez un client.");
            }

            if (Lines.Count == 0)
            {
                throw new BusinessRuleException("Ajoutez au moins une ligne.");
            }

            var lines = Lines.Select(line => line.ToLine()).ToList();
            await _invoices.SaveDraftAsync(new InvoiceDraft(InvoiceId, SelectedClient.Id, Subject, IssueDate, DueDate, lines));
        });

        if (saved)
        {
            await _navigator.ShowInvoicesAsync();
            Notifications.ShowInfo("Brouillon enregistré.");
        }
    }

    [RelayCommand]
    private Task CancelAsync() => _navigator.ShowInvoicesAsync();

    partial void OnSelectedClientChanged(Client? value) => SuggestDueDate();

    partial void OnIssueDateChanged(DateOnly value) => SuggestDueDate();

    /// <summary>Propose l'echeance selon le delai du client ; l'utilisateur peut ensuite la changer.</summary>
    private void SuggestDueDate()
    {
        if (!_isLoading && SelectedClient is { } client)
        {
            DueDate = IssueDate.AddDays(client.PaymentTermDays);
        }
    }

    private async Task LoadExistingAsync()
    {
        var invoice = await _invoices.GetAsync(InvoiceId)
            ?? throw new BusinessRuleException("Cette facture n'existe plus.");
        if (invoice.GetStatus(_clock.Today) != InvoiceStatus.Draft)
        {
            throw new BusinessRuleException("Une facture envoyée ne peut plus être modifiée.");
        }

        Title = "Modifier le brouillon";
        Subject = invoice.Subject;
        IssueDate = invoice.IssueDate;
        DueDate = invoice.DueDate;
        SelectedClient = Clients.FirstOrDefault(client => client.Id == invoice.ClientId);
        foreach (var line in invoice.Lines)
        {
            Lines.Add(InvoiceLineViewModel.From(line));
        }
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var line in e.OldItems?.OfType<InvoiceLineViewModel>() ?? [])
        {
            line.PropertyChanged -= OnLineChanged;
        }

        foreach (var line in e.NewItems?.OfType<InvoiceLineViewModel>() ?? [])
        {
            line.PropertyChanged += OnLineChanged;
        }

        NotifyTotals();
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e) => NotifyTotals();

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalExcludingTax));
        OnPropertyChanged(nameof(TotalTax));
        OnPropertyChanged(nameof(TotalIncludingTax));
    }
}
