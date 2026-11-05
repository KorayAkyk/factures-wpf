using System.IO;

namespace Factures.App;

public static class AppPaths
{
    /// <summary>Permet de lancer le logiciel sur une base jetable, par exemple en integration continue.</summary>
    public const string DataDirectoryVariable = "FACTURES_DATA_DIR";

    public static string GetDatabaseFile()
    {
        var directory = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FacturesWpf");
        }

        return Path.Combine(directory, "factures.db");
    }
}
