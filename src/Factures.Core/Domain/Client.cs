namespace Factures.Core.Domain;

public sealed class Client
{
    public const int DefaultPaymentTermDays = 30;

    public Client(string name, string email, int paymentTermDays = DefaultPaymentTermDays)
    {
        Update(name, email, paymentTermDays);
    }

    public int Id { get; set; }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public int PaymentTermDays { get; private set; }

    public void Update(string name, string email, int paymentTermDays)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessRuleException("Le nom du client est obligatoire.");
        }

        var trimmedEmail = email?.Trim() ?? string.Empty;
        if (trimmedEmail.Length > 0 && !LooksLikeEmail(trimmedEmail))
        {
            throw new BusinessRuleException($"L'adresse « {trimmedEmail} » n'est pas valide.");
        }

        if (paymentTermDays is < 0 or > 120)
        {
            throw new BusinessRuleException("Le délai de paiement doit être compris entre 0 et 120 jours.");
        }

        Name = name.Trim();
        Email = trimmedEmail;
        PaymentTermDays = paymentTermDays;
    }

    private static bool LooksLikeEmail(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 && at == value.LastIndexOf('@') && value.IndexOf('.', at) > at + 1 && !value.EndsWith('.');
    }
}
