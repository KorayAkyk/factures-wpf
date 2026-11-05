using Factures.Core.Abstractions;
using Factures.Core.Domain;
using Factures.Core.Services;
using Factures.Presentation.Services;

namespace Factures.Tests.Support;

public sealed class FixedClock(DateOnly today) : IClock
{
    public DateOnly Today { get; set; } = today;
}

public sealed class InMemoryClientRepository : IClientRepository
{
    private readonly List<Client> _clients = [];
    private int _nextId = 1;

    public Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Client>>(_clients.ToList());

    public Task<Client?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_clients.Find(client => client.Id == id));

    public Task SaveAsync(Client client, CancellationToken cancellationToken = default)
    {
        if (client.Id == 0)
        {
            client.Id = _nextId++;
            _clients.Add(client);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        _clients.RemoveAll(client => client.Id == id);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly List<Invoice> _invoices = [];
    private int _nextId = 1;

    /// <summary>Si renseigne, la prochaine lecture echoue : simule une base inaccessible.</summary>
    public Exception? FailNextRead { get; set; }

    public Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (FailNextRead is { } failure)
        {
            FailNextRead = null;
            throw failure;
        }

        return Task.FromResult<IReadOnlyList<Invoice>>(_invoices.ToList());
    }

    public Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_invoices.Find(invoice => invoice.Id == id));

    public Task SaveAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice.Id == 0)
        {
            invoice.Id = _nextId++;
            _invoices.Add(invoice);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        _invoices.RemoveAll(invoice => invoice.Id == id);
        return Task.CompletedTask;
    }
}

public sealed class FakeDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;

    public string? SavePath { get; set; }

    public List<string> Confirmations { get; } = [];

    public bool Confirm(string title, string message)
    {
        Confirmations.Add(message);
        return ConfirmResult;
    }

    public string? AskCsvSavePath(string suggestedFileName) => SavePath;
}

public sealed class FakeFileWriter : IFileWriter
{
    public Exception? Failure { get; set; }

    public Dictionary<string, byte[]> Written { get; } = [];

    public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Written[path] = content;
        return Task.CompletedTask;
    }
}

public sealed class FakeNavigator : INavigator
{
    public List<string> Calls { get; } = [];

    public Task ShowDashboardAsync() => Record("dashboard");

    public Task ShowInvoicesAsync(InvoiceStatus? status = null) => Record($"invoices:{status}");

    public Task ShowClientsAsync() => Record("clients");

    public Task EditInvoiceAsync(int? invoiceId) => Record($"edit:{invoiceId}");

    private Task Record(string call)
    {
        Calls.Add(call);
        return Task.CompletedTask;
    }
}

/// <summary>Tout le graphe en memoire, date au 15 juin 2026.</summary>
public sealed class TestWorld
{
    public static readonly DateOnly Today = new(2026, 6, 15);

    public TestWorld()
    {
        Invoices = new InvoiceService(InvoiceRepository, ClientRepository, Clock);
        Clients = new ClientService(ClientRepository, InvoiceRepository);
    }

    public FixedClock Clock { get; } = new(Today);

    public InMemoryClientRepository ClientRepository { get; } = new();

    public InMemoryInvoiceRepository InvoiceRepository { get; } = new();

    public InvoiceService Invoices { get; }

    public ClientService Clients { get; }

    public FakeDialogService Dialogs { get; } = new();

    public FakeFileWriter Files { get; } = new();

    public FakeNavigator Navigator { get; } = new();

    public NotificationCenter Notifications { get; } = new();

    public async Task<Client> AddClientAsync(string name, int paymentTermDays = 30)
    {
        var client = new Client(name, string.Empty, paymentTermDays);
        await ClientRepository.SaveAsync(client);
        return client;
    }

    /// <summary>Ajoute une facture 100 HT a 20 % dans l'etat demande, relativement a aujourd'hui.</summary>
    public async Task<Invoice> AddInvoiceAsync(Client client, InvoiceStatus status, string subject = "Prestation", decimal unitPrice = 100m)
    {
        var issued = status == InvoiceStatus.Overdue ? Today.AddDays(-60) : Today.AddDays(-5);
        var invoice = new Invoice(client.Id, subject, issued, issued.AddDays(30), [new InvoiceLine("Ligne", 1, unitPrice, 20)]);
        await InvoiceRepository.SaveAsync(invoice);
        if (status != InvoiceStatus.Draft)
        {
            invoice.MarkSent($"FAC-2026-{invoice.Id:0000}", issued);
        }

        if (status == InvoiceStatus.Paid)
        {
            invoice.MarkPaid(Today.AddDays(-1));
        }

        return invoice;
    }
}
