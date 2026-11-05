using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Factures.Core.Domain;
using Factures.Core.Export;
using Factures.Core.Queries;
using Factures.Core.Services;
using Factures.Presentation.Services;

namespace Factures.Presentation.ViewModels;

public sealed partial class InvoiceListViewModel : PageViewModel
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private readonly InvoiceService _invoices;
    private readonly ClientService _clients;
    private readonly INavigator _navigator;
    private readonly IDialogService _dialogs;
    private readonly IFileWriter _files;
    private IReadOnlyList<InvoiceRow> _allRows = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(MarkPaidCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private InvoiceRow? _selectedInvoice;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    public InvoiceListViewModel(
        InvoiceService invoices,
        ClientService clients,
        INavigator navigator,
        IDialogService dialogs,
        IFileWriter files,
        NotificationCenter notifications)
        : base(notifications, AppSection.Invoices, "Factures")
    {
        _invoices = invoices;
        _clients = clients;
        _navigator = navigator;
        _dialogs = dialogs;
        _files = files;
        Filters.Changed += (_, _) => ApplyFilters();
    }

    public InvoiceFilterViewModel Filters { get; } = new();

    public ObservableCollection<InvoiceRow> Results { get; } = [];

    public override Task LoadAsync() => RunAsync(ReloadAsync);

    private bool CanEdit => SelectedInvoice is { Status: InvoiceStatus.Draft };

    private bool CanSend => SelectedInvoice is { Status: InvoiceStatus.Draft };

    private bool CanMarkPaid => SelectedInvoice is { Status: InvoiceStatus.Sent or InvoiceStatus.Overdue };

    private bool CanDelete => SelectedInvoice is { Status: InvoiceStatus.Draft };

    private bool CanExportCsv => Results.Count > 0;

    [RelayCommand]
    private Task NewAsync() => _navigator.EditInvoiceAsync(null);

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private Task EditAsync() => _navigator.EditInvoiceAsync(SelectedInvoice!.Id);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var id = SelectedInvoice!.Id;
        var question = "Envoyer cette facture ? Un numéro définitif lui sera attribué et elle ne pourra plus être modifiée.";
        if (!_dialogs.Confirm("Envoyer la facture", question))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var number = await _invoices.SendAsync(id);
            await ReloadAsync();
            Notifications.ShowInfo($"Facture {number} marquée comme envoyée.");
        });
    }

    [RelayCommand(CanExecute = nameof(CanMarkPaid))]
    private async Task MarkPaidAsync()
    {
        var row = SelectedInvoice!;
        if (!_dialogs.Confirm("Enregistrer le paiement", $"Marquer la facture {row.DisplayNumber} comme payée aujourd'hui ?"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _invoices.MarkPaidAsync(row.Id);
            await ReloadAsync();
            Notifications.ShowInfo($"Facture {row.DisplayNumber} marquée comme payée.");
        });
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        var id = SelectedInvoice!.Id;
        if (!_dialogs.Confirm("Supprimer le brouillon", "Supprimer définitivement ce brouillon ?"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _invoices.DeleteDraftAsync(id);
            await ReloadAsync();
            Notifications.ShowInfo("Brouillon supprimé.");
        });
    }

    /// <summary>Exporte ce qui est affiche : les filtres en cours s'appliquent au fichier.</summary>
    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private async Task ExportCsvAsync()
    {
        var path = _dialogs.AskCsvSavePath("factures.csv");
        if (path is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _files.WriteAllBytesAsync(path, InvoiceCsvExporter.ToBytes(Results));
            Notifications.ShowInfo($"{Results.Count} facture(s) exportée(s) dans {path}.");
        });
    }

    private async Task ReloadAsync()
    {
        var clients = await _clients.GetAllSortedAsync();
        _allRows = await _invoices.GetRowsAsync();
        Filters.SetClients(clients);
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var previousId = SelectedInvoice?.Id;
        var matching = _allRows.Apply(Filters.ToFilter()).ToList();

        Results.Clear();
        foreach (var row in matching)
        {
            Results.Add(row);
        }

        SelectedInvoice = matching.Find(row => row.Id == previousId);
        ExportCsvCommand.NotifyCanExecuteChanged();

        var total = matching.Sum(row => row.TotalIncludingTax);
        ResultSummary = string.Create(French, $"{matching.Count} facture(s), {total:C2} TTC");
    }
}
