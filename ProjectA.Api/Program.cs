using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Attachments;
using ProjectA.Api.Features.Auth;
using ProjectA.Api.Features.Bookmarks;
using ProjectA.Api.Features.Categories;
using ProjectA.Api.Features.Notes;
using ProjectA.Api.Features.Projects;
using ProjectA.Api.Features.ToDo;
using ProjectA.Api.Features.Users;
using ProjectA.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";

        if (context.Exception != null)
        {
      
        }
    };
});
//builder.Services.AddHealthChecks()
//   .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
//    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new PostgresDbConnectionFactory(builder.Configuration.GetConnectionString("DefaultConnection")!));

// Auth: JwtTokenService (used by the login endpoint to mint tokens) and the JwtBearer handler
// (used by [RequireAuthorization] on Create/Update/Delete endpoints to validate them) read the
// same three Jwt:* settings. Those reads happen INSIDE the AddJwtBearer configure callback below,
// not as local variables up here, because this callback only actually runs the first time the
// scheme handles a request - well after builder.Build(). WebApplicationFactory-based tests inject
// their config overrides (a test-only signing key) exactly at Build(), so reading eagerly here
// would capture appsettings.json's placeholder value into the closure instead, permanently out of
// sync with what JwtTokenService (a DI singleton, constructed after Build()) actually signs with.
builder.Services.AddSingleton<JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtIssuer = builder.Configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var jwtAudience = builder.Configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
        var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

//app.MapHealthChecks("/health", new HealthCheckOptions
//    {
//        Predicate = registration => registration.Tags.Contains("live")
 //   })
//    .ExcludeFromDescription();

//app.MapHealthChecks("/ready", new HealthCheckOptions
//   {
//        Predicate = registration => registration.Tags.Contains("ready")
//    })
//    .ExcludeFromDescription();

app.MapToDoEndpoints();
app.MapCategoryEndpoints();
app.MapBookmarkEndpoints();
app.MapNoteEndpoints();
app.MapAttachmentEndpoints();
app.MapProjectEndpoints();
app.MapAuthEndpoints();
app.MapUserEndpoints();

await SeedAdminUserIfNoneExistAsync(app.Services, app.Configuration, app.Logger);

app.Run();

// Ensures there's always at least one working login out of the box, without a public
// registration endpoint to provision the very first account. Only ever inserts a row when the
// users table is completely empty, so it's a no-op on every startup after the first.
static async Task SeedAdminUserIfNoneExistAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
{
    try
    {
        var connectionFactory = services.GetRequiredService<IDbConnectionFactory>();
        using var connection = await connectionFactory.CreateConnectionAsync();

        var existingUserCount = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM users;");
        if (existingUserCount > 0)
        {
            return;
        }

        var username = configuration["Seed:AdminUsername"] ?? "admin";
        var password = configuration["Seed:AdminPassword"] ?? "ChangeMe123!";

        await connection.ExecuteAsync(
            "INSERT INTO users (username, password_hash, date_created) VALUES (@Username, @PasswordHash, now());",
            new { Username = username, PasswordHash = PasswordHasher.Hash(password) });

        logger.LogWarning(
            "Seeded an initial user {Username} because the users table was empty. Log in and " +
            "create a real account, then remove or change the Seed:AdminPassword configuration.",
            username);
    }
    catch (Exception ex)
    {
        // Startup shouldn't crash the whole API just because seeding couldn't run (e.g. the
        // database isn't reachable yet, or the users table doesn't exist because migrations
        // haven't been applied) - logging in will simply fail until that's fixed.
        logger.LogError(ex, "Could not seed the initial admin user.");
    }
}

public partial class Program;
