using ProjectA.Web.Common;
using ProjectA.Web.Models;

namespace ProjectA.Web.Services;

public interface IAuthApiClient
{
    Task<ApiResult<LoginResult>> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
}

// Deliberately not one of the typed clients that gets BearerTokenHandler attached - logging in
// is how a token is obtained in the first place, so this call is always anonymous.
public sealed class AuthApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IAuthApiClient
{
    public Task<ApiResult<LoginResult>> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
        PostJsonAsync<LoginRequestBody, LoginResult>(
            "api/auth/login",
            new LoginRequestBody(username, password),
            cancellationToken);

    private sealed record LoginRequestBody(string Username, string Password);
}
