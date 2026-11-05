using Factures.Core.Abstractions;
using Factures.Core.Domain;
using Factures.Core.Services;

namespace Factures.Core.Demo;

/// <summary>
/// Remplit une base vide avec des donnees entierement inventees, datees par rapport
/// au jour de lancement pour que chaque statut soit visible des la premiere ouverture.
/// </summary>
public sealed class DemoDataSeeder(IClientRepository clients, IInvoiceRepository invoices, IClock clock)
{
    private sealed record Sample(int Client, string Subject, int DaysAgo, int? PaidAfterDays, bool Sent, InvoiceLine[] Lines);

    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        if ((await clients.GetAllAsync(cancellationToken)).Count > 0)
        {
            return;
        }

        var savedClients = new List<Client>();
        foreach (var client in BuildClients())
        {
            await clients.SaveAsync(client, cancellationToken);
            savedClients.Add(client);
        }

        var today = clock.Today;
        var numbers = new List<string?>();
        foreach (var sample in BuildSamples().OrderByDescending(sample => sample.DaysAgo))
        {
            var client = savedClients[sample.Client];
            var issued = today.AddDays(-sample.DaysAgo);
            var invoice = new Invoice(client.Id, sample.Subject, issued, issued.AddDays(client.PaymentTermDays), sample.Lines);
            if (sample.Sent)
            {
                var number = InvoiceNumberGenerator.Next(numbers, issued.Year);
                numbers.Add(number);
                invoice.MarkSent(number, issued);
            }

            if (sample.PaidAfterDays is { } delay)
            {
                invoice.MarkPaid(issued.AddDays(delay));
            }

            await invoices.SaveAsync(invoice, cancellationToken);
        }
    }

    private static Client[] BuildClients() =>
    [
        new("Atelier Lumen (fictif)", "contact@atelier-lumen.example", 30),
        new("Boulangerie des Trois Épis (fictif)", "bonjour@trois-epis.example", 15),
        new("Cabinet Orme & Associés (fictif)", "compta@cabinet-orme.example", 45),
        new("École de Voile Noroît (fictif)", "accueil@voile-noroit.example", 30),
        new("Studio Parallaxe (fictif)", "projets@studio-parallaxe.example", 30),
    ];

    private static Sample[] BuildSamples() =>
    [
        new(0, "Refonte du catalogue en ligne", 200, 25, true, [Line("Conception", 4, 450), Line("Développement", 8, 480)]),
        new(1, "Site vitrine de la boulangerie", 165, 12, true, [Line("Maquettes", 2, 400), Line("Intégration", 3, 420)]),
        new(2, "Audit de sécurité du poste client", 140, 40, true, [Line("Audit", 3, 650), Line("Rapport", 1, 500)]),
        new(3, "Module de réservation des stages", 110, 28, true, [Line("Développement", 6, 480)]),
        new(4, "Automatisation des exports", 85, 20, true, [Line("Scripts", 5, 450), Line("Formation", 1, 380, 0)]),
        new(0, "Maintenance trimestrielle", 60, 15, true, [Line("Forfait maintenance", 1, 900)]),
        new(2, "Tableau de suivi des dossiers", 95, null, true, [Line("Développement", 4, 520)]),
        new(1, "Paiement en ligne des commandes", 72, null, true, [Line("Intégration", 2, 480), Line("Tests", 1, 350)]),
        new(3, "Application de planning", 50, null, true, [Line("Développement", 3, 480)]),
        new(4, "Portail client", 14, null, true, [Line("Conception", 2, 450), Line("Développement", 5, 480)]),
        new(0, "Évolution du catalogue", 6, null, true, [Line("Développement", 3, 480)]),
        new(3, "Refonte du formulaire d'inscription", 2, null, false, [Line("Estimation", 2, 480)]),
        new(4, "Migration vers le nouveau serveur", 0, null, false, [Line("Migration", 1, 1200), Line("Suivi", 2, 300)]),
    ];

    private static InvoiceLine Line(string description, decimal quantity, decimal unitPrice, decimal vatRate = 20) =>
        new(description, quantity, unitPrice, vatRate);
}
