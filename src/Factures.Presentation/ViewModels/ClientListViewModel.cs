using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Factures.Core.Domain;
using Factures.Core.Services;
using Factures.Presentation.Services;

namespace Factures.Presentation.ViewModels;

public sealed partial class ClientListViewModel(
    ClientService clients,
    IDialogService dialogs,
    NotificationCenter notifications)
    : PageViewModel(notifications, AppSection.Clients, "Clients")
{
    private int _editingId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private Client? _selectedClient;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private int _paymentTermDays = Client.DefaultPaymentTermDays;

    [ObservableProperty]
    private string _formTitle = "Nouveau client";

    public ObservableCollection<Client> Clients { get; } = [];

    public override Task LoadAsync() => RunAsync(() => ReloadAsync(null));

    private bool CanDelete => SelectedClient is not null;

    [RelayCommand]
    private void New() => SelectedClient = null;

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            var saved = await clients.SaveAsync(_editingId, Name, Email, PaymentTermDays);
            await ReloadAsync(saved.Id);
            Notifications.ShowInfo($"Client « {saved.Name} » enregistré.");
        });
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        var client = SelectedClient!;
        if (!dialogs.Confirm("Supprimer le client", $"Supprimer définitivement « {client.Name} » ?"))
        {
            return;
        }

        await RunAsync(async () =>
        {
            await clients.DeleteAsync(client.Id);
            await ReloadAsync(null);
            Notifications.ShowInfo($"Client « {client.Name} » supprimé.");
        });
    }

    partial void OnSelectedClientChanged(Client? value)
    {
        _editingId = value?.Id ?? 0;
        Name = value?.Name ?? string.Empty;
        Email = value?.Email ?? string.Empty;
        PaymentTermDays = value?.PaymentTermDays ?? Client.DefaultPaymentTermDays;
        FormTitle = value is null ? "Nouveau client" : "Modifier le client";
    }

    private async Task ReloadAsync(int? selectId)
    {
        var all = await clients.GetAllSortedAsync();
        Clients.Clear();
        foreach (var client in all)
        {
            Clients.Add(client);
        }

        SelectedClient = all.FirstOrDefault(client => client.Id == selectId);
    }
}
