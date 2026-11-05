namespace Factures.Core.Domain;

/// <summary>
/// Regle metier non respectee. Le message est en francais et destine a l'utilisateur.
/// </summary>
public sealed class BusinessRuleException(string message) : Exception(message);
