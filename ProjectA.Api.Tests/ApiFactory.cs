using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ProjectA.Api.Data;
using ProjectA.Api.Schema;
using Testcontainers.PostgreSql;
using Xunit;

namespace ProjectA.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<IApiMarker>, IAsyncLifetime
{
    // Credentials for the one user the startup seeder creates (the users table is empty on a
    // fresh Testcontainers database), and the Jwt:* settings needed to issue/validate tokens for
    // it - set explicitly here via ConfigureAppConfiguration rather than relying on
    // appsettings.json's production placeholders.
    private const string TestAdminUsername = "test-admin";
    private const string TestAdminPassword = "Test-Password-123!";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HttpClient? HttpClient { get; private set; }

    private string? _authToken;
    private readonly SemaphoreSlim _authTokenLock = new(1, 1);

    private readonly PostgreSqlContainer _postgresDbContainer = new PostgreSqlBuilder("postgres:17.10-alpine")
        .WithDatabase("projectaDb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithPortBinding(5432, 5432)
        .WithWaitStrategy((DotNet.Testcontainers.Configurations.IWaitForContainerOS)Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5432))
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminUsername"] = TestAdminUsername,
                ["Seed:AdminPassword"] = TestAdminPassword,
                ["Jwt:Issuer"] = "ProjectA.Api.Tests",
                ["Jwt:Audience"] = "ProjectA.Api.Tests",
                ["Jwt:SigningKey"] = "test-only-signing-key-0123456789abcdefABCDEF-not-for-production",
                ["Jwt:ExpiryMinutes"] = "120"
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(IDbConnectionFactory));

            var connectionString = _postgresDbContainer.GetConnectionString();
            //disable GSS Encryption to avoid 'Error: libgssapi_krb5.so.2: cannot open shared object file:'
            //https://www.npgsql.org/doc/security.html#gss-session-encryption-gss-api
            connectionString += ";GSS Encryption Mode=Disable";

            services.AddSingleton<IDbConnectionFactory>(_ =>
                new PostgresDbConnectionFactory(connectionString)
            );
        });

        var connectString = _postgresDbContainer.GetConnectionString();
        connectString += ";GSS Encryption Mode=Disable";

        // Upgrader.Upgrade() returns false rather than throwing on a failed script, so this
        // has to be checked explicitly - otherwise a broken migration fails silently and every
        // test just runs against whatever incomplete schema was left behind.
        if (!Upgrader.Upgrade(connectString))
        {
            throw new InvalidOperationException(
                "Database migration failed - check console output from DbUp for the failing script.");
        }
    }

    // Every Create/Update/Delete endpoint now requires an authenticated caller, so tests that
    // exercise them need more than the plain CreateClient() this factory used to hand out.
    // This logs in once (as the seeded test-admin user) and caches the token, so every test
    // class can just call this instead without each one needing its own login dance.
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetAuthToken());
        return client;
    }

    private string GetAuthToken()
    {
        if (_authToken is not null)
        {
            return _authToken;
        }

        _authTokenLock.Wait();
        try
        {
            if (_authToken is not null)
            {
                return _authToken;
            }

            using var loginClient = CreateClient();
            var response = loginClient
                .PostAsJsonAsync(
                    "/api/auth/login",
                    new { Username = TestAdminUsername, Password = TestAdminPassword },
                    JsonOptions)
                .GetAwaiter()
                .GetResult();

            response.EnsureSuccessStatusCode();

            var payload = response.Content
                .ReadFromJsonAsync<LoginResponsePayload>(JsonOptions)
                .GetAwaiter()
                .GetResult();

            _authToken = payload!.Token;
            return _authToken;
        }
        finally
        {
            _authTokenLock.Release();
        }
    }

    private sealed record LoginResponsePayload(string Token, DateTime ExpiresAtUtc, string Username);

    public async Task InitializeAsync()
    {
        await _postgresDbContainer.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgresDbContainer.StopAsync();
        await _postgresDbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}