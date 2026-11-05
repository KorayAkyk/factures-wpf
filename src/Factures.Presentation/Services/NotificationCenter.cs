using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Factures.Presentation.Services;

/// <summary>Message affiche dans le bandeau de la fenetre principale.</summary>
public sealed partial class NotificationCenter : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    [ObservableProperty]
    private bool _isError;

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    /// <summary>Une information ne remplace jamais une erreur que l'utilisateur n'a pas encore vue.</summary>
    public void ShowInfo(string message)
    {
        if (IsError && HasMessage)
        {
            return;
        }

        IsError = false;
        Message = message;
    }

    public void ShowError(string message)
    {
        IsError = true;
        Message = message;
    }

    [RelayCommand]
    public void Clear()
    {
        IsError = false;
        Message = null;
    }
}
