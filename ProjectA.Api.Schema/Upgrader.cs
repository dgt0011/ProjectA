using System.Reflection;
using DbUp;
using DbUp.Postgresql;

namespace ProjectA.Api.Schema;

public static class Upgrader
{
    public static bool Upgrade(string connectionString)
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString);
        
        var builder = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly());

        builder.WithVariablesDisabled();

        builder.Configure(
            c => c.Journal = new PostgresqlTableJournal(() => c.ConnectionManager, () => c.Log, "public", "schemaversions"));
        
        var upgrader = builder.Build();

        var result = upgrader.PerformUpgrade();
        return result.Successful;
    }
}