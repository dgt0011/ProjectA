using System.Data;
using Npgsql;

namespace ProjectA.Api.Data;

public sealed class PostgresDbConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}