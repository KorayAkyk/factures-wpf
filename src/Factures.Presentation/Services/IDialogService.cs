namespace Factures.Presentation.Services;

/// <summary>Boites de dialogue, derriere une interface pour que les ViewModels restent testables.</summary>
public interface IDialogService
{
    bool Confirm(string title, string message);

    /// <summary>Demande ou enregistrer un CSV. Retourne null si l'utilisateur annule.</summary>
    string? AskCsvSavePath(string suggestedFileName);
}
