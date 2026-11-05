using System.Globalization;
using System.IO;
using System.Windows;
using Factures.App.Converters;
using Factures.Core.Abstractions;
using Factures.Presentation.Services;
using Factures.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Factures.App.Tests;

public class ConverterAndCompositionTests
{
    private readonly DateOnlyToDateTimeConverter _converter = new();

    [Fact]
    public void DateOnly_converts_to_midnight_for_the_date_picker()
    {
        var result = _converter.Convert(new DateOnly(2026, 6, 15), typeof(DateTime?), null, CultureInfo.InvariantCulture);

        Assert.Equal(new DateTime(2026, 6, 15), result);
    }

    [Fact]
    public void Null_date_stays_null_for_the_date_picker()
    {
        Assert.Null(_converter.Convert(null, typeof(DateTime?), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Chosen_date_converts_back_ignoring_the_time()
    {
        var result = _converter.ConvertBack(new DateTime(2026, 6, 15, 13, 30, 0), typeof(DateOnly), null, CultureInfo.InvariantCulture);

        Assert.Equal(new DateOnly(2026, 6, 15), result);
    }

    [Fact]
    public void Clearing_the_picker_empties_an_optional_date()
    {
        Assert.Null(_converter.ConvertBack(null, typeof(DateOnly?), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Clearing_the_picker_leaves_a_required_date_unchanged()
    {
        var result = _converter.ConvertBack(null, typeof(DateOnly), null, CultureInfo.InvariantCulture);

        Assert.Same(DependencyProperty.UnsetValue, result);
    }

    [Fact]
    public void Container_resolves_the_whole_graph_with_shared_navigation_and_fresh_pages()
    {
        var file = Path.Combine(Path.GetTempPath(), "factures-di-" + Guid.NewGuid().ToString("N"), "test.db");
        using var services = Bootstrapper.BuildServices(file);

        Assert.IsType<SystemClock>(services.GetRequiredService<IClock>());
        Assert.Same(services.GetRequiredService<NavigationService>(), services.GetRequiredService<INavigator>());
        Assert.NotNull(services.GetRequiredService<MainViewModel>());

        var factory = services.GetRequiredService<Func<InvoiceListViewModel>>();
        Assert.NotSame(factory(), factory());
    }

    [Fact]
    public void Data_directory_variable_redirects_the_database()
    {
        var previous = Environment.GetEnvironmentVariable(AppPaths.DataDirectoryVariable);
        var directory = Path.Combine(Path.GetTempPath(), "factures-chemin");
        try
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, directory);
            Assert.Equal(Path.Combine(directory, "factures.db"), AppPaths.GetDatabaseFile());

            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, null);
            Assert.EndsWith(Path.Combine("FacturesWpf", "factures.db"), AppPaths.GetDatabaseFile());
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirectoryVariable, previous);
        }
    }
}
