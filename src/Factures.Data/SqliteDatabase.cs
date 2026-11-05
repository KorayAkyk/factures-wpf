using Microsoft.Data.Sqlite;

namespace Factures.Data;

/// <summary>Point d'acces unique a la base locale : ouvre les connexions et applique le schema.</summary>
public sealed class SqliteDatabase(string filePath)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = filePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        ForeignKeys = true,
        Pooling = false,
    }.ToString();

    public string FilePath { get; } = filePath;

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>Cree le dossier et applique les migrations manquantes, dans l'ordre.</summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await OpenAsync(cancellationToken);
        var current = await GetSchemaVersionAsync(connection, cancellationToken);
        for (var version = current; version < SchemaMigrations.All.Count; version++)
        {
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"{SchemaMigrations.All[version]}\nPRAGMA user_version = {version + 1};";
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public static async Task<int> GetSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }
}
