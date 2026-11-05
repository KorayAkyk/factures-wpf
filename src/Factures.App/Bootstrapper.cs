using Factures.App.Services;
using Factures.Core.Abstractions;
using Factures.Core.Demo;
using Factures.Core.Services;
using Factures.Data;
using Factures.Presentation.Services;
using Factures.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Factures.App;

/// <summary>Seul endroit qui connait a la fois les interfaces et leurs implementations.</summary>
public static class Bootstrapper
{
    public static ServiceProvider BuildServices(string databaseFile)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(new SqliteDatabase(databaseFile));
        services.AddSingleton<IClientRepository, SqliteClientRepository>();
        services.AddSingleton<IInvoiceRepository, SqliteInvoiceRepository>();
        services.AddSingleton<InvoiceService>();
        services.AddSingleton<ClientService>();
        services.AddSingleton<DemoDataSeeder>();

        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<IFileWriter, DiskFileWriter>();
        services.AddSingleton<NotificationCenter>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigator>(provider => provider.GetRequiredService<NavigationService>());
        services.AddSingleton<MainViewModel>();

        AddPage<DashboardViewModel>(services);
        AddPage<InvoiceListViewModel>(services);
        AddPage<InvoiceEditorViewModel>(services);
        AddPage<ClientListViewModel>(services);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    public static async Task PrepareDatabaseAsync(IServiceProvider services)
    {
        await services.GetRequiredService<SqliteDatabase>().MigrateAsync();
        await services.GetRequiredService<DemoDataSeeder>().SeedIfEmptyAsync();
    }

    /// <summary>Une nouvelle instance a chaque navigation, creee a la demande par une fabrique.</summary>
    private static void AddPage<TPage>(IServiceCollection services)
        where TPage : PageViewModel
    {
        services.AddTransient<TPage>();
        services.AddSingleton<Func<TPage>>(provider => provider.GetRequiredService<TPage>);
    }
}
