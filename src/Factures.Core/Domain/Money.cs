namespace Factures.Core.Domain;

/// <summary>Arrondi commercial unique pour tous les montants de l'application.</summary>
public static class Money
{
    public static decimal Round(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static decimal ExcludingTax(decimal quantity, decimal unitPrice) =>
        Round(quantity * unitPrice);

    public static decimal Tax(decimal amountExcludingTax, decimal vatRatePercent) =>
        Round(amountExcludingTax * vatRatePercent / 100m);
}
