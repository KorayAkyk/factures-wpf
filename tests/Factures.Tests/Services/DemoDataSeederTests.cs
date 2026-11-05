using Factures.Core.Demo;
using Factures.Core.Domain;
using Factures.Tests.Support;

namespace Factures.Tests.Services;

public class DemoDataSeederTests
{
    private readonly TestWorld _world = new();

    private DemoDataSeeder Seeder => new(_world.ClientRepository, _world.InvoiceRepository, _world.Clock);

    [Fact]
    public async Task Seed_shows_every_status()
    {
        await Seeder.SeedIfEmptyAsync();

        var statuses = (await _world.Invoices.GetRowsAsync()).Select(row => row.Status).Distinct();

        Assert.Equal(
            [InvoiceStatus.Draft, InvoiceStatus.Sent, InvoiceStatus.Paid, InvoiceStatus.Overdue],
            statuses.OrderBy(status => status));
    }

    [Fact]
    public async Task Seed_runs_only_on_an_empty_base()
    {
        await Seeder.SeedIfEmptyAsync();
        var firstCount = (await _world.InvoiceRepository.GetAllAsync()).Count;

        await Seeder.SeedIfEmptyAsync();

        Assert.Equal(firstCount, (await _world.InvoiceRepository.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Seed_leaves_an_existing_base_untouched()
    {
        await _world.AddClientAsync("Vrai client");

        await Seeder.SeedIfEmptyAsync();

        Assert.Single(await _world.ClientRepository.GetAllAsync());
        Assert.Empty(await _world.InvoiceRepository.GetAllAsync());
    }

    [Fact]
    public async Task Demo_clients_are_marked_fictional_and_use_a_reserved_domain()
    {
        await Seeder.SeedIfEmptyAsync();

        Assert.All(await _world.ClientRepository.GetAllAsync(), client =>
        {
            Assert.Contains("(fictif)", client.Name);
            Assert.EndsWith(".example", client.Email);
        });
    }

    [Fact]
    public async Task Demo_numbers_are_unique_and_never_dated_in_the_future()
    {
        await Seeder.SeedIfEmptyAsync();
        var invoices = await _world.InvoiceRepository.GetAllAsync();

        var numbers = invoices.Where(invoice => invoice.Number is not null).Select(invoice => invoice.Number).ToList();

        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.All(invoices, invoice => Assert.True(invoice.PaidOn is null || invoice.PaidOn <= TestWorld.Today));
        Assert.All(invoices, invoice => Assert.True(invoice.IssueDate <= TestWorld.Today));
    }
}
