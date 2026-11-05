using System.Globalization;
using Factures.Core.Abstractions;
using Factures.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Factures.Data;

public sealed class SqliteInvoiceRepository(SqliteDatabase database) : IInvoiceRepository
{
    private const string SelectInvoices =
        "SELECT id, number, client_id, subject, issue_date, due_date, sent_on, paid_on FROM invoices";

    public async Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        var linesByInvoice = (await ReadLinesAsync(connection, null, cancellationToken))
            .ToLookup(entry => entry.InvoiceId, entry => entry.Line);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectInvoices} ORDER BY id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var invoices = new List<Invoice>();
        while (await reader.ReadAsync(cancellationToken))
        {
            invoices.Add(ReadInvoice(reader, linesByInvoice[reader.GetInt32(0)]));
        }

        return invoices;
    }

    public async Task<Invoice?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        var lines = (await ReadLinesAsync(connection, id, cancellationToken)).Select(entry => entry.Line);

        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectInvoices} WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadInvoice(reader, lines) : null;
    }

    /// <summary>Facture et lignes sont ecrites dans la meme transaction : tout ou rien.</summary>
    public async Task SaveAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        var id = await UpsertInvoiceAsync(connection, transaction, invoice, cancellationToken);
        await ReplaceLinesAsync(connection, transaction, id, invoice.Lines, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        invoice.Id = id;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM invoices WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> UpsertInvoiceAsync(
        SqliteConnection connection, SqliteTransaction transaction, Invoice invoice, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = invoice.Id == 0
            ? """
              INSERT INTO invoices (number, client_id, subject, issue_date, due_date, sent_on, paid_on)
              VALUES ($number, $client, $subject, $issue, $due, $sent, $paid);
              SELECT last_insert_rowid();
              """
            : """
              UPDATE invoices SET number = $number, client_id = $client, subject = $subject,
                  issue_date = $issue, due_date = $due, sent_on = $sent, paid_on = $paid
              WHERE id = $id;
              SELECT $id;
              """;
        command.Parameters.AddWithValue("$id", invoice.Id);
        command.Parameters.AddWithValue("$number", invoice.Number.ToSqlOrNull());
        command.Parameters.AddWithValue("$client", invoice.ClientId);
        command.Parameters.AddWithValue("$subject", invoice.Subject);
        command.Parameters.AddWithValue("$issue", invoice.IssueDate.ToSql());
        command.Parameters.AddWithValue("$due", invoice.DueDate.ToSql());
        command.Parameters.AddWithValue("$sent", invoice.SentOn.ToSqlOrNull());
        command.Parameters.AddWithValue("$paid", invoice.PaidOn.ToSqlOrNull());

        var id = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id, CultureInfo.InvariantCulture);
    }

    private static async Task ReplaceLinesAsync(
        SqliteConnection connection, SqliteTransaction transaction, int invoiceId,
        IReadOnlyList<InvoiceLine> lines, CancellationToken cancellationToken)
    {
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM invoice_lines WHERE invoice_id = $id;";
            delete.Parameters.AddWithValue("$id", invoiceId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        for (var position = 0; position < lines.Count; position++)
        {
            var line = lines[position];
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO invoice_lines (invoice_id, position, description, quantity, unit_price, vat_rate)
                VALUES ($invoice, $position, $description, $quantity, $price, $vat);
                """;
            insert.Parameters.AddWithValue("$invoice", invoiceId);
            insert.Parameters.AddWithValue("$position", position);
            insert.Parameters.AddWithValue("$description", line.Description);
            insert.Parameters.AddWithValue("$quantity", line.Quantity.ToSql());
            insert.Parameters.AddWithValue("$price", line.UnitPrice.ToSql());
            insert.Parameters.AddWithValue("$vat", line.VatRate.ToSql());
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<List<(int InvoiceId, InvoiceLine Line)>> ReadLinesAsync(
        SqliteConnection connection, int? invoiceId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var where = invoiceId is null ? string.Empty : "WHERE invoice_id = $id";
        command.CommandText = $"""
            SELECT invoice_id, description, quantity, unit_price, vat_rate
            FROM invoice_lines {where} ORDER BY invoice_id, position;
            """;
        if (invoiceId is { } id)
        {
            command.Parameters.AddWithValue("$id", id);
        }

        var result = new List<(int, InvoiceLine)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add((reader.GetInt32(0), new InvoiceLine(
                reader.GetString(1),
                reader.GetDecimalText("quantity"),
                reader.GetDecimalText("unit_price"),
                reader.GetDecimalText("vat_rate"))));
        }

        return result;
    }

    private static Invoice ReadInvoice(SqliteDataReader reader, IEnumerable<InvoiceLine> lines) =>
        Invoice.Restore(
            reader.GetInt32(0),
            reader.GetNullableString("number"),
            reader.GetInt32(2),
            reader.GetString(3),
            reader.GetDate("issue_date"),
            reader.GetDate("due_date"),
            reader.GetNullableDate("sent_on"),
            reader.GetNullableDate("paid_on"),
            lines);
}
