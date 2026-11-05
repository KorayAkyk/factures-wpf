namespace Factures.Core.Domain;

/// <summary>Ligne de facture immuable : une ligne invalide ne peut pas exister.</summary>
public sealed record InvoiceLine
{
    public InvoiceLine(string description, decimal quantity, decimal unitPrice, decimal vatRate)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new BusinessRuleException("Chaque ligne doit avoir une description.");
        }

        if (quantity <= 0)
        {
            throw new BusinessRuleException($"La quantité de « {description} » doit être positive.");
        }

        if (unitPrice < 0)
        {
            throw new BusinessRuleException($"Le prix de « {description} » ne peut pas être négatif.");
        }

        if (vatRate is < 0 or > 100)
        {
            throw new BusinessRuleException($"Le taux de TVA de « {description} » doit être entre 0 et 100 %.");
        }

        Description = description.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatRate = vatRate;
    }

    public string Description { get; }

    public decimal Quantity { get; }

    public decimal UnitPrice { get; }

    /// <summary>Taux de TVA en pourcentage (20 pour 20 %).</summary>
    public decimal VatRate { get; }

    public decimal TotalExcludingTax => Money.ExcludingTax(Quantity, UnitPrice);

    public decimal Tax => Money.Tax(TotalExcludingTax, VatRate);

    public decimal TotalIncludingTax => TotalExcludingTax + Tax;
}
