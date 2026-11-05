using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Factures.App.Views;
using Factures.Core.Domain;
using Factures.Presentation.Services;
using Factures.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Factures.App.Tests;

/// <summary>
/// Charge chaque vue avec le vrai graphe de services et une vraie base de demonstration.
/// Une faute dans le XAML ou dans le chemin d'une liaison fait echouer ces tests.
/// </summary>
public sealed class ViewsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "factures-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task<ServiceProvider> BuildPreparedServicesAsync()
    {
        var services = Bootstrapper.BuildServices(Path.Combine(_directory, "test.db"));
        await Bootstrapper.PrepareDatabaseAsync(services);
        return services;
    }

    private static void Render(FrameworkElement element, double width = 1200, double height = 780)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
        WpfTestHost.Flush();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static DataGridRow[] RealizedRows(DependencyObject root) => Descendants<DataGridRow>(root).ToArray();

    [Fact]
    public Task Collector_really_detects_a_broken_binding() => WpfTestHost.RunAsync(() =>
    {
        // Temoin : sans lui, les tests ci-dessous pourraient passer sans rien controler.
        using var collector = new BindingErrorCollector();
        var text = new TextBlock { DataContext = new object() };
        text.SetBinding(TextBlock.TextProperty, new Binding("ProprieteInexistante"));

        Render(text);

        Assert.Contains(collector.Errors, error => error.Contains("ProprieteInexistante"));
        return Task.CompletedTask;
    });

    [Fact]
    public Task Dashboard_view_renders_without_binding_error() => WpfTestHost.RunAsync(async () =>
    {
        using var services = await BuildPreparedServicesAsync();
        using var collector = new BindingErrorCollector();
        var viewModel = services.GetRequiredService<DashboardViewModel>();
        await viewModel.LoadAsync();

        var view = new DashboardView { DataContext = viewModel };
        Render(view);

        Assert.Empty(collector.Errors);
        Assert.NotEmpty(RealizedRows(view));
    });

    [Fact]
    public Task Invoice_list_view_renders_rows_and_status_badges() => WpfTestHost.RunAsync(async () =>
    {
        using var services = await BuildPreparedServicesAsync();
        using var collector = new BindingErrorCollector();
        var viewModel = services.GetRequiredService<InvoiceListViewModel>();
        await viewModel.LoadAsync();

        var view = new InvoiceListView { DataContext = viewModel };
        Render(view);

        Assert.Empty(collector.Errors);
        Assert.NotEmpty(RealizedRows(view));
        var labels = Descendants<TextBlock>(view).Select(text => text.Text).ToList();
        Assert.Contains(InvoiceStatus.Overdue.ToFrench(), labels);
        Assert.Contains("Aucune date", labels);
    });

    [Fact]
    public Task Invoice_editor_view_renders_lines_and_vat_choices() => WpfTestHost.RunAsync(async () =>
    {
        using var services = await BuildPreparedServicesAsync();
        using var collector = new BindingErrorCollector();
        var viewModel = services.GetRequiredService<InvoiceEditorViewModel>();
        await viewModel.LoadAsync();
        viewModel.AddLineCommand.Execute(null);

        var view = new InvoiceEditorView { DataContext = viewModel };
        Render(view);

        Assert.Empty(collector.Errors);
        Assert.Equal(2, RealizedRows(view).Length);
        var vatCombo = Descendants<ComboBox>(view).First(combo => combo.ItemsSource == viewModel.VatRates);
        Assert.Equal(20m, vatCombo.SelectedItem);
    });

    [Fact]
    public Task Client_list_view_renders_without_binding_error() => WpfTestHost.RunAsync(async () =>
    {
        using var services = await BuildPreparedServicesAsync();
        using var collector = new BindingErrorCollector();
        var viewModel = services.GetRequiredService<ClientListViewModel>();
        await viewModel.LoadAsync();

        var view = new ClientListView { DataContext = viewModel };
        Render(view);

        Assert.Empty(collector.Errors);
        Assert.Equal(5, RealizedRows(view).Length);
    });

    [Fact]
    public Task Main_window_shows_the_page_chosen_by_navigation() => WpfTestHost.RunAsync(async () =>
    {
        using var services = await BuildPreparedServicesAsync();
        using var collector = new BindingErrorCollector();
        var viewModel = services.GetRequiredService<MainViewModel>();
        await viewModel.InitializeCommand.ExecuteAsync(null);

        var window = new MainWindow { DataContext = viewModel };
        var root = (FrameworkElement)window.Content;
        window.Content = null;
        root.DataContext = viewModel;
        Render(root);

        Assert.Empty(collector.Errors);
        Assert.Single(Descendants<DashboardView>(root));

        await services.GetRequiredService<INavigator>().ShowClientsAsync();
        Render(root);

        Assert.Empty(Descendants<DashboardView>(root));
        Assert.Single(Descendants<ClientListView>(root));
        Assert.Empty(collector.Errors);
        window.Close();
    });

    [Fact]
    public Task Error_is_visible_in_the_banner_of_the_main_window() => WpfTestHost.RunAsync(async () =>
    {
        using var services = await BuildPreparedServicesAsync();
        var viewModel = services.GetRequiredService<MainViewModel>();
        await viewModel.InitializeCommand.ExecuteAsync(null);

        var window = new MainWindow { DataContext = viewModel };
        var root = (FrameworkElement)window.Content;
        window.Content = null;
        root.DataContext = viewModel;
        Render(root);
        var banner = Descendants<Border>(root).Single(
            border => System.Windows.Automation.AutomationProperties.GetAutomationId(border) == "notification-banner");
        Assert.Equal(Visibility.Collapsed, banner.Visibility);

        viewModel.Notifications.ShowError("Message de test");
        Render(root);

        Assert.Equal(Visibility.Visible, banner.Visibility);
        Assert.Contains(Descendants<TextBlock>(banner), text => text.Text == "Message de test");
        window.Close();
    });

    [Fact]
    public Task Every_theme_resource_used_by_the_grid_styles_is_resolved() => WpfTestHost.RunAsync(() =>
    {
        foreach (var key in new[] { "ReadOnlyGrid", "AppDataGrid", "PrimaryButton", "DangerButton", "NavButton", "StatusBadgeTemplate", "BoolToVisibility", "DateOnlyToDateTime" })
        {
            Assert.NotNull(Application.Current.FindResource(key));
        }

        return Task.CompletedTask;
    });
}
