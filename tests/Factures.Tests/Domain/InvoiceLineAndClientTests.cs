using Factures.Core.Domain;

namespace Factures.Tests.Domain;

public class InvoiceLineAndClientTests
{
    [Theory]
    [InlineData("", 1, 10, 20)]
    [InlineData("   ", 1, 10, 20)]
    [InlineData("Ligne", 0, 10, 20)]
    [InlineData("Ligne", -1, 10, 20)]
    [InlineData("Ligne", 1, -0.01, 20)]
    [InlineData("Ligne", 1, 10, -1)]
    [InlineData("Ligne", 1, 10, 101)]
    public void Invalid_line_is_refused(string description, double quantity, double unitPrice, double vatRate)
    {
        Assert.Throws<BusinessRuleException>(() =>
            new InvoiceLine(description, (decimal)quantity, (decimal)unitPrice, (decimal)vatRate));
    }

    [Theory]
    [InlineData(1, 1.005, 0, 1.01, 0)]
    [InlineData(1, 1.004, 0, 1.00, 0)]
    [InlineData(2.5, 100, 5.5, 250, 13.75)]
    [InlineData(1, 0.25, 10, 0.25, 0.03)]
    public void Line_amounts_are_rounded_commercially(
        double quantity, double unitPrice, double vatRate, double expectedExcludingTax, double expectedTax)
    {
        var line = new InvoiceLine("Ligne", (decimal)quantity, (decimal)unitPrice, (decimal)vatRate);

        Assert.Equal((decimal)expectedExcludingTax, line.TotalExcludingTax);
        Assert.Equal((decimal)expectedTax, line.Tax);
        Assert.Equal((decimal)expectedExcludingTax + (decimal)expectedTax, line.TotalIncludingTax);
    }

    [Fact]
    public void Line_description_is_trimmed()
    {
        Assert.Equal("Audit", new InvoiceLine("  Audit ", 1, 1, 0).Description);
    }

    [Theory]
    [InlineData("contact@exemple.example")]
    [InlineData("")]
    public void Client_accepts_a_valid_or_missing_email(string email)
    {
        Assert.Equal(email, new Client("Nom", email).Email);
    }

    [Theory]
    [InlineData("sans-arobase.example")]
    [InlineData("deux@@exemple.example")]
    [InlineData("@exemple.example")]
    [InlineData("contact@exemple")]
    [InlineData("contact@exemple.")]
    public void Client_refuses_a_malformed_email(string email)
    {
        Assert.Throws<BusinessRuleException>(() => new Client("Nom", email));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    public void Client_refuses_a_payment_term_out_of_range(int days)
    {
        Assert.Throws<BusinessRuleException>(() => new Client("Nom", string.Empty, days));
    }

    [Fact]
    public void Refused_update_leaves_the_client_unchanged()
    {
        var client = new Client("Nom", "a@b.example", 30);

        Assert.Throws<BusinessRuleException>(() => client.Update(" ", "a@b.example", 30));

        Assert.Equal("Nom", client.Name);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft, "Brouillon")]
    [InlineData(InvoiceStatus.Sent, "Envoyée")]
    [InlineData(InvoiceStatus.Paid, "Payée")]
    [InlineData(InvoiceStatus.Overdue, "En retard")]
    public void Every_status_has_a_french_label(InvoiceStatus status, string expected)
    {
        Assert.Equal(expected, status.ToFrench());
    }
}
