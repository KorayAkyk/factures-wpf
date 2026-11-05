using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Factures.Core.Abstractions;
using Factures.Core.Domain;
using Factures.Core.Reports;
using Factures.Core.Services;
using Factures.Presentation.Services;

namespace Factures.Presentation.ViewModels;

/// <summary>Une barre du graphique des encaissements, prete a afficher.</summary>
public sealed record MonthBar(string Label, decimal Amount, double Percent);

public sealed partial class DashboardViewModel(
    InvoiceService invoices,
    IClock clock,
    INavigator navigator,
    NotificationCenter notifications)
    : PageViewModel(notifications, AppSection.Dashboard, "Tableau de bord")
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOverdue), nameof(HasNoOverdue))]
    private DashboardSummary? _summary;

    public bool HasOverdue => Summary is { OverdueCount: > 0 };

    public bool HasNoOverdue => Summary is { OverdueCount: 0 };

    public ObservableCollection<MonthBar> Months { get; } = [];

    public override Task LoadAsync() => RunAsync(async () =>
    {
        var rows = await invoices.GetRowsAsync();
        var summary = DashboardCalculator.Compute(rows, clock.Today);

        var highest = summary.LastSixMonths.Max(month => month.Amount);
        Months.Clear();
        foreach (var month in summary.LastSixMonths)
        {
            Months.Add(new MonthBar(
                new DateOnly(month.Year, month.Month, 1).ToString("MMM yyyy", French),
                month.Amount,
                highest == 0 ? 0 : (double)(month.Amount / highest * 100)));
        }

        Summary = summary;
    });

    [RelayCommand]
    private Task ShowOverdueAsync() => navigator.ShowInvoicesAsync(InvoiceStatus.Overdue);
}
