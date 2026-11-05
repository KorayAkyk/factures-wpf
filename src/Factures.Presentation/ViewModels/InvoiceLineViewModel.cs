using CommunityToolkit.Mvvm.ComponentModel;
using Factures.Core.Domain;

namespace Factures.Presentation.ViewModels;

/// <summary>Ligne en cours de saisie. Elle peut etre incomplete : le domaine la valide a l'enregistrement.</summary>
public sealed partial class InvoiceLineViewModel : ObservableObject
{
    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalExcludingTax), nameof(Tax))]
    private decimal _quantity = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalExcludingTax), nameof(Tax))]
    private decimal _unitPrice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Tax))]
    private decimal _vatRate = 20;

    public decimal TotalExcludingTax => Money.ExcludingTax(Quantity, UnitPrice);

    public decimal Tax => Money.Tax(TotalExcludingTax, VatRate);

    public InvoiceLine ToLine() => new(Description, Quantity, UnitPrice, VatRate);

    public static InvoiceLineViewModel From(InvoiceLine line) => new()
    {
        Description = line.Description,
        Quantity = line.Quantity,
        UnitPrice = line.UnitPrice,
        VatRate = line.VatRate,
    };
}
