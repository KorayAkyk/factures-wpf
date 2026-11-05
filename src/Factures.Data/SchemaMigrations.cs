namespace Factures.Data;

/// <summary>
/// Scripts de schema, un par version (PRAGMA user_version). On ajoute a la fin, on ne modifie jamais.
/// Les montants sont stockes en texte decimal : SQLite n'a pas de type decimal et un REAL les arrondirait.
/// </summary>
internal static class SchemaMigrations
{
    public static readonly IReadOnlyList<string> All =
    [
        """
        CREATE TABLE clients (
            id                INTEGER PRIMARY KEY AUTOINCREMENT,
            name              TEXT NOT NULL,
            email             TEXT NOT NULL,
            payment_term_days INTEGER NOT NULL
        );

        CREATE TABLE invoices (
            id         INTEGER PRIMARY KEY AUTOINCREMENT,
            number     TEXT UNIQUE,
            client_id  INTEGER NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
            subject    TEXT NOT NULL,
            issue_date TEXT NOT NULL,
            due_date   TEXT NOT NULL,
            sent_on    TEXT,
            paid_on    TEXT
        );

        CREATE TABLE invoice_lines (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            invoice_id  INTEGER NOT NULL REFERENCES invoices(id) ON DELETE CASCADE,
            position    INTEGER NOT NULL,
            description TEXT NOT NULL,
            quantity    TEXT NOT NULL,
            unit_price  TEXT NOT NULL,
            vat_rate    TEXT NOT NULL
        );

        CREATE INDEX ix_invoices_client ON invoices(client_id);
        CREATE INDEX ix_invoice_lines_invoice ON invoice_lines(invoice_id);
        """,
    ];
}
