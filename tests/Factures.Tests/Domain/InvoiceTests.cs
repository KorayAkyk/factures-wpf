using Factures.Core.Domain;

namespace Factures.Tests.Domain;

public class InvoiceTests
{
    private static readonly DateOnly Issue = new(2026, 3, 1);
    private static readonly DateOnly Due = new(2026, 3, 31);

    private static Invoice NewInvoice(params InvoiceLine[] lines) =>
        new(1, "Objet", Issue, Due, lines.Length > 0 ? lines : [new InvoiceLine("Ligne", 2, 100, 20)]);

    [Fact]
    public void Status_is_draft_until_sent()
    {
        Assert.Equal(InvoiceStatus.Draft, NewInvoice().GetStatus(Due.AddDays(100)));
    }

    [Fact]
    public void Sent_invoice_is_not_overdue_on_its_due_date()
    {
        var invoice = NewInvoice();
        invoice.MarkSent("FAC-1", Issue);

        Assert.Equal(InvoiceStatus.Sent, invoice.GetStatus(Due));
        Assert.Equal(0, invoice.GetDaysOverdue(Due));
    }

    [Fact]
    public void Sent_invoice_becomes_overdue_the_day_after_its_due_date()
    {
        var invoice = NewInvoice();
        invoice.MarkSent("FAC-1", Issue);

        Assert.Equal(InvoiceStatus.Overdue, invoice.GetStatus(Due.AddDays(1)));
        Assert.Equal(10, invoice.GetDaysOverdue(Due.AddDays(10)));
    }

    [Fact]
    public void Paid_invoice_is_never_overdue_even_if_paid_late()
    {
        var invoice = NewInvoice();
        invoice.MarkSent("FAC-1", Issue);
        invoice.MarkPaid(Due.AddDays(20));

        Assert.Equal(InvoiceStatus.Paid, invoice.GetStatus(Due.AddDays(40)));
        Assert.Equal(0, invoice.GetDaysOverdue(Due.AddDays(40)));
    }

    [Fact]
    public void Totals_add_rounded_lines_rather_than_rounding_the_sum()
    {
        // 3 x 0,335 = 1,005 arrondi a 1,01 par ligne ; la TVA de chaque ligne (0,202) arrondie a 0,20.
        var invoice = NewInvoice(new InvoiceLine("A", 3, 0.335m, 20), new InvoiceLine("B", 3, 0.335m, 20));

        Assert.Equal(2.02m, invoice.TotalExcludingTax);
        Assert.Equal(0.40m, invoice.TotalTax);
        Assert.Equal(2.42m, invoice.TotalIncludingTax);
    }

    [Fact]
    public void Sent_invoice_cannot_be_edited()
    {
        var invoice = NewInvoice();
        invoice.MarkSent("FAC-1", Issue);

        var error = Assert.Throws<BusinessRuleException>(() =>
            invoice.UpdateDraft(1, "Autre objet", Issue, Due, invoice.Lines));

        Assert.Contains("ne peut plus être modifiée", error.Message);
        Assert.Equal("Objet", invoice.Subject);
    }

    [Fact]
    public void Invoice_without_amount_cannot_be_sent()
    {
        var empty = new Invoice(1, "Objet", Issue, Due, []);
        var free = NewInvoice(new InvoiceLine("Offert", 1, 0, 20));

        Assert.Throws<BusinessRuleException>(() => empty.MarkSent("FAC-1", Issue));
        Assert.Throws<BusinessRuleException>(() => free.MarkSent("FAC-1", Issue));
        Assert.Null(empty.Number);
    }

    [Fact]
    public void Invoice_cannot_be_sent_twice_and_keeps_its_first_number()
    {
        var invoice = NewInvoice();
        invoice.MarkSent("FAC-1", Issue);

        Assert.Throws<BusinessRuleException>(() => invoice.MarkSent("FAC-2", Issue));
        Assert.Equal("FAC-1", invoice.Number);
    }

    [Fact]
    public void Draft_cannot_be_marked_paid()
    {
        Assert.Throws<BusinessRuleException>(() => NewInvoice().MarkPaid(Due));
    }

    [Fact]
    public void Payment_cannot_precede_sending()
    {
        var invoice = NewInvoice();
        invoice.MarkSent("FAC-1", Issue);

        Assert.Throws<BusinessRuleException>(() => invoice.MarkPaid(Issue.AddDays(-1)));
        Assert.Null(invoice.PaidOn);
    }

    [Fact]
    public void Due_date_cannot_precede_issue_date()
    {
        Assert.Throws<BusinessRuleException>(() => new Invoice(1, "Objet", Due, Issue, []));
    }

    [Theory]
    [InlineData(0, "Objet")]
    [InlineData(1, "   ")]
    public void Draft_requires_a_client_and_a_subject(int clientId, string subject)
    {
        Assert.Throws<BusinessRuleException>(() => new Invoice(clientId, subject, Issue, Due, []));
    }

    [Fact]
    public void Restore_brings_back_the_state_without_replaying_the_rules()
    {
        var restored = Invoice.Restore(7, "FAC-7", 1, "Objet", Issue, Due, Issue, Due, [new InvoiceLine("L", 1, 10, 0)]);

        Assert.Equal(7, restored.Id);
        Assert.Equal(InvoiceStatus.Paid, restored.GetStatus(Due));
        Assert.Equal(10m, restored.TotalIncludingTax);
    }
}
