using System.Globalization;
using System.Text.Json;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

using Npgsql;

namespace ProjectA.Api.Security;

// On EC2 (or anywhere AwsSecrets:Enabled is true - see appsettings.Production.json) the
// database connection string and JWT signing key are pulled from AWS Secrets Manager instead
// of appsettings.json, using whatever credentials the AWS SDK's default credential chain
// finds. On EC2 that's the IAM role attached to the instance profile, resolved automatically
// via the instance metadata service - no access key/secret is configured anywhere in this app.
//
// Timing matters here more than almost anything else in Program.cs: the database connection
// string is read as a synchronous top-level statement (`builder.Configuration
// .GetConnectionString("DefaultConnection")`) to build IDbConnectionFactory and run DbUp,
// well before builder.Build() - so this loader has to run, and finish, before that line, or
// the eager read will just see whatever placeholder is (or isn't) in appsettings.json. Jwt:
// SigningKey is read lazily inside the AddJwtBearer configure callback instead (see the
// comment there), so it isn't as time-critical - but both are loaded here, together, for one
// single startup path rather than two different injection points.
//
// AwsSecrets:Enabled defaults to false in the base appsettings.json, so local development and
// the test suite (both of which run under the Development environment - WebApplicationFactory
// defaults to Development, and appsettings.Development.json doesn't override this) never
// attempt an AWS call. It's only flipped on in appsettings.Production.json.
internal static class AwsSecretsLoader
{
    public static async Task LoadIntoConfigurationAsync(
        ConfigurationManager configuration,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("AwsSecrets:Enabled"))
        {
            return;
        }

        var region = configuration["AwsSecrets:Region"];
        var dbConnectionSecretName = configuration["AwsSecrets:DbConnectionSecretName"]
            ?? throw new InvalidOperationException("AwsSecrets:DbConnectionSecretName is not configured.");
        var jwtSigningKeySecretName = configuration["AwsSecrets:JwtSigningKeySecretName"]
            ?? throw new InvalidOperationException("AwsSecrets:JwtSigningKeySecretName is not configured.");

        using var client = string.IsNullOrWhiteSpace(region)
            ? new AmazonSecretsManagerClient()
            : new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

        var connectionStringTask = GetDbConnectionStringAsync(client, dbConnectionSecretName, cancellationToken);
        var jwtSigningKeyTask = GetSecretStringAsync(client, jwtSigningKeySecretName, cancellationToken);
        await Task.WhenAll(connectionStringTask, jwtSigningKeyTask);

        // Added as an in-memory provider on top of everything CreateBuilder(args) already
        // loaded (appsettings.json, appsettings.{Environment}.json, environment variables) -
        // config providers added later win, so these values take priority over whatever
        // placeholder ended up in appsettings.json, exactly as intended.
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionStringTask.Result,
            ["Jwt:SigningKey"] = jwtSigningKeyTask.Result
        });
    }

    // ProjectADBConnection's SecretString is a JSON object with host/port/dbname/username/
    // password fields (the same shape AWS's own "credentials for RDS database" secret
    // templates use) - assembled into an Npgsql connection string via
    // NpgsqlConnectionStringBuilder rather than raw string interpolation, so a password
    // containing characters like ';' or '=' can't corrupt the resulting connection string.
    private static async Task<string> GetDbConnectionStringAsync(
        IAmazonSecretsManager client, string secretName, CancellationToken cancellationToken)
    {
        var json = await GetSecretStringAsync(client, secretName, cancellationToken);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Secrets Manager secret '{secretName}' does not contain valid JSON.", ex);
        }

        using (document)
        {
            var root = document.RootElement;

            var connectionStringBuilder = new NpgsqlConnectionStringBuilder
            {
                Host = RequireString(root, "host", secretName),
                Port = RequirePort(root, secretName),
                Database = RequireString(root, "dbname", secretName),
                Username = RequireString(root, "username", secretName),
                Password = RequireString(root, "password", secretName),
                GssEncryptionMode = GssEncryptionMode.Disable
            };
            
            var retVal = connectionStringBuilder.ConnectionString;
            // this *really* shouldnt be necessary
            retVal = retVal.Replace("Database=postgres;", "Database=postgresdb;");
            
            return retVal;
        }
    }

    private static async Task<string> GetSecretStringAsync(
        IAmazonSecretsManager client, string secretName, CancellationToken cancellationToken)
    {
        var response = await client.GetSecretValueAsync(
            new GetSecretValueRequest { SecretId = secretName },
            cancellationToken);

        if (string.IsNullOrEmpty(response.SecretString))
        {
            throw new InvalidOperationException($"Secrets Manager secret '{secretName}' has no SecretString value.");
        }

        return response.SecretString;
    }

    private static string RequireString(JsonElement root, string propertyName, string secretName)
    {
        if (!root.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException(
                $"Secrets Manager secret '{secretName}' is missing a non-empty '{propertyName}' field.");
        }

        return value.GetString()!;
    }

    // AWS's own auto-generated database secrets store "port" as a JSON number (e.g. 5432), not
    // a string, so this has to accept either representation.
    private static int RequirePort(JsonElement root, string secretName)
    {
        if (!root.TryGetProperty("port", out var value))
        {
            throw new InvalidOperationException($"Secrets Manager secret '{secretName}' is missing a 'port' field.");
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.Number when value.TryGetInt32(out var numericPort):
                return numericPort;
            case JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort):
                return parsedPort;
            default:
                throw new InvalidOperationException(
                    $"Secrets Manager secret '{secretName}' has an invalid 'port' field - expected a number or numeric string.");
        }
    }
}
