using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Factures.App.Views;
using Factures.Presentation.Services;
using Factures.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Factures.App;

/// <summary>Racine de composition : demarrage, culture et filet de securite pour les erreurs.</summary>
public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigureFrenchCulture();

        try
        {
            _services = Bootstrapper.BuildServices(AppPaths.GetDatabaseFile());
            await Bootstrapper.PrepareDatabaseAsync(_services);

            DispatcherUnhandledException += OnUnhandledException;
            var viewModel = _services.GetRequiredService<MainViewModel>();
            new MainWindow { DataContext = viewModel }.Show();
            await viewModel.InitializeCommand.ExecuteAsync(null);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Le logiciel n'a pas pu démarrer.\n\n{exception.Message}",
                "Factures WPF",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _services?.GetRequiredService<NotificationCenter>()
            .ShowError($"Une erreur inattendue est survenue : {e.Exception.Message}");
        e.Handled = true;
    }

    private static void ConfigureFrenchCulture()
    {
        var french = CultureInfo.GetCultureInfo("fr-FR");
        CultureInfo.DefaultThreadCurrentCulture = french;
        CultureInfo.DefaultThreadCurrentUICulture = french;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(french.IetfLanguageTag)));
    }
}
