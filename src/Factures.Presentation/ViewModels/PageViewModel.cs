using CommunityToolkit.Mvvm.ComponentModel;
using Factures.Core.Domain;
using Factures.Presentation.Services;

namespace Factures.Presentation.ViewModels;

public enum AppSection
{
    Dashboard,
    Invoices,
    Clients,
}

/// <summary>Base des pages : etat occupe et transformation des erreurs en message visible.</summary>
public abstract partial class PageViewModel(NotificationCenter notifications, AppSection section, string title) : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = title;

    public AppSection Section { get; } = section;

    protected NotificationCenter Notifications { get; } = notifications;

    public abstract Task LoadAsync();

    /// <summary>
    /// Execute une action. Aucune erreur ne remonte : elle est montree a l'utilisateur.
    /// Retourne false si l'action a echoue.
    /// </summary>
    protected async Task<bool> RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
            return true;
        }
        catch (BusinessRuleException exception)
        {
            Notifications.ShowError(exception.Message);
            return false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Notifications.ShowError($"L'opération a échoué : {exception.Message}");
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
