using System.Net.Http.Headers;
using ProjectA.Web.Security;

namespace ProjectA.Web.Services;

// Attached to every typed HttpClient that calls ProjectA.Api (except IAuthApiClient itself,
// which is what obtains the token in the first place). Reads the bearer token from the
// signed-in user's claims and forwards it as the Authorization header, so Create/Update/
// Delete calls succeed against the API's [RequireAuthorization] endpoints.
public sealed class BearerTokenHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = httpContextAccessor.HttpContext?.User.FindFirst(AuthConstants.ApiTokenClaimType)?.Value;

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
