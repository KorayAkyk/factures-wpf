using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace Factures.App.Tests;

/// <summary>
/// Un seul thread STA et une seule Application pour tous les tests, car WPF n'en autorise qu'une
/// par processus. Les ressources de l'application sont chargees comme au demarrage reel.
/// </summary>
public static class WpfTestHost
{
    private static readonly Lazy<Dispatcher> LazyDispatcher = new(StartUiThread);

    public static Task RunAsync(Func<Task> test) =>
        LazyDispatcher.Value.InvokeAsync(test).Task.Unwrap();

    /// <summary>Laisse WPF terminer liaisons et mise en page en attente.</summary>
    public static void Flush() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static Dispatcher StartUiThread()
    {
        using var ready = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;
        var thread = new Thread(() =>
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            application.Resources = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/FacturesWpf;component/Themes/Theme.xaml"),
            };
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        ready.Wait();
        return dispatcher!;
    }
}

/// <summary>Collecte les erreurs de liaison, celles qui sinon restent muettes a l'execution.</summary>
public sealed class BindingErrorCollector : TraceListener
{
    public BindingErrorCollector()
    {
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(this);
    }

    public List<string> Errors { get; } = [];

    public override void Write(string? message)
    {
    }

    public override void WriteLine(string? message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            Errors.Add(message);
        }
    }

    protected override void Dispose(bool disposing)
    {
        PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
        base.Dispose(disposing);
    }
}
