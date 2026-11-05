using System.Text;
using Factures.Core.Domain;
using Factures.Core.Export;
using Factures.Core.Queries;

namespace Factures.Tests.Export;

public class InvoiceCsvExporterTests
{
    private static InvoiceRow Row(string client = "Client", string subject = "Objet", decimal total = 1234.5m, string? paidOn = null) =>
        new(1, "FAC-2026-0001", 1, client, subject, new DateOnly(2026, 2, 3), new DateOnly(2026, 3, 5),
            paidOn is null ? null : DateOnly.Parse(paidOn), total, total * 1.2m, InvoiceStatus.Sent, 0);

    private static string[] Lines(params InvoiceRow[] rows) =>
        InvoiceCsvExporter.ToCsv(rows).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Header_and_row_use_french_excel_conventions()
    {
        var lines = Lines(Row());

        Assert.Equal("Numéro;Client;Objet;Date;Échéance;Statut;Total HT;Total TTC;Payée le", lines[0]);
        Assert.Equal("FAC-2026-0001;Client;Objet;03/02/2026;05/03/2026;Envoyée;1234,50;1481,40;", lines[1]);
    }

    [Fact]
    public void Amounts_do_not_depend_on_the_machine_culture()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        try
        {
            Assert.Contains(";1234,50;", Lines(Row())[1]);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Separator_quote_and_line_break_are_escaped()
    {
        var line = InvoiceCsvExporter.ToCsv([Row(client: "Dupont; fils", subject: "Dit \"urgent\"\nsuite")]);

        Assert.Contains(";\"Dupont; fils\";\"Dit \"\"urgent\"\"\nsuite\";", line);
    }

    [Theory]
    [InlineData("=SOMME(A1)", "'=SOMME(A1)")]
    [InlineData("+33", "'+33")]
    [InlineData("-1", "'-1")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("Normal=ok", "Normal=ok")]
    public void Formula_cells_are_neutralised(string client, string expected)
    {
        Assert.Equal(expected, Lines(Row(client: client))[1].Split(';')[1]);
    }

    [Fact]
    public void Formula_is_neutralised_even_when_the_cell_must_be_quoted()
    {
        Assert.Contains(";\"'=A1;B2\";", Lines(Row(client: "=A1;B2"))[1]);
    }

    [Fact]
    public void Payment_date_is_filled_when_paid()
    {
        Assert.EndsWith(";01/04/2026", Lines(Row(paidOn: "2026-04-01"))[1]);
    }

    [Fact]
    public void Bytes_start_with_a_utf8_bom_so_excel_keeps_the_accents()
    {
        var bytes = InvoiceCsvExporter.ToBytes([Row()]);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        Assert.Contains("Échéance", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void No_row_still_gives_the_header()
    {
        Assert.Single(Lines());
    }
}
