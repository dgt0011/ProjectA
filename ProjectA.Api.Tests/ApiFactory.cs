using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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
    public HttpClient? HttpClient { get; private set; }
    
    private readonly PostgreSqlContainer _postgresDbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:17.10-alpine")
        .WithDatabase("projectaDb")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithPortBinding(5432, 5432)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();
   
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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

        Upgrader.Upgrade(connectString);
    }
    
    public async Task InitializeAsync()
    {
        await _postgresDbContainer.StartAsync();
        //HttpClient = CreateClient();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgresDbContainer.StopAsync();
        await _postgresDbContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}