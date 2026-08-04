using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ProjectA.Api.Data;
using ProjectA.Api.Features.Auth.Login;
using ProjectA.Api.Features.Users.ChangePassword;
using ProjectA.Api.Features.Users.CreateUser;
using Xunit;

namespace ProjectA.Api.Tests.Features.Users.ChangePassword;

[Collection(nameof(ApiCollection))]
public class ChangePasswordEndpointTests : IAsyncLifetime
{
    // Deliberately never mutates the shared test-admin account (its bearer token is cached
    // process-wide and reused by every other test class via CreateAuthenticatedClient()) -
    // each test provisions its own throwaway user via the Create endpoint instead, and logs
    // in as that user directly, exactly the way a real self-service caller would.
    private const string UsernamePrefix = "changepwd-test";
    private const string InitialPassword = "Original-Password-1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiFactory _factory;
    private readonly HttpClient _adminClient;
    private readonly IDbConnectionFactory _connectionFactory;

    public ChangePasswordEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _adminClient = factory.CreateAuthenticatedClient();
        _connectionFactory = factory.Services.GetRequiredService<IDbConnectionFactory>();
    }

    public async Task InitializeAsync() => await CleanUpAsync();

    public async Task DisposeAsync() => await CleanUpAsync();

    private async Task CleanUpAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM users WHERE username LIKE @Pattern;",
            new { Pattern = $"{UsernamePrefix}%" });
    }

    // Creates a fresh user (via the admin-authenticated client, mirroring how accounts are
    // actually provisioned in this app) and returns an HttpClient authenticated as that user's
    // own bearer token - i.e. what a signed-in caller changing their own password would have.
    private async Task<(string Username, HttpClient Client)> CreateUserAndLogInAsync(string usernameSuffix)
    {
        var username = $"{UsernamePrefix}-{usernameSuffix}-{Guid.NewGuid():N}";

        var createResponse = await _adminClient.PostAsJsonAsync(
            "/api/users",
            new CreateUserEndpoint.CreateUserRequest(username, InitialPassword),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var loginResponse = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginEndpoint.LoginRequest(username, InitialPassword),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginEndpoint.LoginResponse>(JsonOptions);
        Assert.NotNull(login);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        return (username, client);
    }

    [Fact]
    public async Task Post_WithCorrectCurrentPassword_ChangesPasswordAndReturnsOk()
    {
        var (username, client) = await CreateUserAndLogInAsync("success");
        const string newPassword = "Brand-New-Password-2";

        var response = await client.PostAsJsonAsync(
            "/api/users/change-password",
            new ChangePasswordEndpoint.ChangePasswordRequest(InitialPassword, newPassword),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ChangePasswordEndpoint.ChangePasswordResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(username, result.Username);
        Assert.NotNull(result.DateModified);

        // The old password no longer works...
        var oldLoginResponse = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginEndpoint.LoginRequest(username, InitialPassword),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);

        // ...but the new one does.
        var newLoginResponse = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginEndpoint.LoginRequest(username, newPassword),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, newLoginResponse.StatusCode);
    }

    [Fact]
    public async Task Post_WithWrongCurrentPassword_ReturnsValidationProblemAndDoesNotChangeAnything()
    {
        var (username, client) = await CreateUserAndLogInAsync("wrongcurrent");

        var response = await client.PostAsJsonAsync(
            "/api/users/change-password",
            new ChangePasswordEndpoint.ChangePasswordRequest("not-the-right-password", "Some-New-Password-1"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("CurrentPassword"));

        // The original password still works - nothing was changed.
        var loginResponse = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginEndpoint.LoginRequest(username, InitialPassword),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Post_WithNewPasswordTooShort_ReturnsValidationProblem()
    {
        var (_, client) = await CreateUserAndLogInAsync("shortnew");

        var response = await client.PostAsJsonAsync(
            "/api/users/change-password",
            new ChangePasswordEndpoint.ChangePasswordRequest(InitialPassword, "short"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(JsonOptions);
        Assert.NotNull(problem);
        Assert.True(problem.Errors.ContainsKey("NewPassword"));
    }

    [Fact]
    public async Task Post_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/users/change-password",
            new ChangePasswordEndpoint.ChangePasswordRequest(InitialPassword, "Some-New-Password-1"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_ChangesOnlyTheCallersOwnAccount_NotTheAdminAccountUsedToProvisionIt()
    {
        // Even though the user was created by _adminClient, changing that new user's own
        // password must never be able to reach back and touch the admin account that created
        // it - the endpoint identifies "self" purely from the caller's own bearer token.
        var (_, client) = await CreateUserAndLogInAsync("scoped");

        var response = await client.PostAsJsonAsync(
            "/api/users/change-password",
            new ChangePasswordEndpoint.ChangePasswordRequest(InitialPassword, "Some-New-Password-1"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // _adminClient's cached bearer token would keep authenticating regardless of whether
        // the admin's password_hash actually changed - JWT validation is stateless and never
        // looks at the database. The only way to prove the admin account's stored credential
        // is genuinely untouched is a fresh login attempt with its real password.
        var adminLoginResponse = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginEndpoint.LoginRequest(ApiFactory.TestAdminUsername, ApiFactory.TestAdminPassword),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, adminLoginResponse.StatusCode);
    }

    private sealed record ValidationProblemResponse(
        string? Type,
        string? Title,
        int? Status,
        Dictionary<string, string[]> Errors);
}
