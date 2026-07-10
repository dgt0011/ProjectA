using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectA.Web.Common;

// Shared plumbing for the per-entity typed HttpClients in Services/. Method names are
// deliberately distinct from the public CRUD methods each derived client exposes (e.g.
// DeleteResourceAsync vs. DeleteAsync) so a derived class declaring its own DeleteAsync
// doesn't hide these protected helpers by name.
public abstract class ApiClientBase(HttpClient httpClient)
{
    protected HttpClient HttpClient { get; } = httpClient;

    protected async Task<ApiResult<T>> GetJsonAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        return await GuardAsync(
            async () =>
            {
                using var response = await HttpClient.GetAsync(requestUri, cancellationToken);
                return await ReadResultAsync<T>(response, cancellationToken);
            },
            ApiResult<T>.Failure);
    }

    protected async Task<ApiResult<TResponse>> PostJsonAsync<TRequest, TResponse>(
        string requestUri, TRequest body, CancellationToken cancellationToken)
    {
        return await GuardAsync(
            async () =>
            {
                using var response = await HttpClient.PostAsJsonAsync(requestUri, body, ApiJson.Options, cancellationToken);
                return await ReadResultAsync<TResponse>(response, cancellationToken);
            },
            ApiResult<TResponse>.Failure);
    }

    protected async Task<ApiResult<TResponse>> PutJsonAsync<TRequest, TResponse>(
        string requestUri, TRequest body, CancellationToken cancellationToken)
    {
        return await GuardAsync(
            async () =>
            {
                using var response = await HttpClient.PutAsJsonAsync(requestUri, body, ApiJson.Options, cancellationToken);
                return await ReadResultAsync<TResponse>(response, cancellationToken);
            },
            ApiResult<TResponse>.Failure);
    }

    protected async Task<ApiResult> DeleteResourceAsync(string requestUri, CancellationToken cancellationToken)
    {
        return await GuardAsync(
            async () =>
            {
                using var response = await HttpClient.DeleteAsync(requestUri, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return ApiResult.Success((int)response.StatusCode);
                }

                var problem = await TryReadProblemAsync(response, cancellationToken);
                return ApiResult.Failure((int)response.StatusCode, problem?.Title, problem?.Detail, problem?.Errors);
            },
            ApiResult.Failure);
    }

    // The API being unreachable (not running, wrong ApiSettings:BaseUrl, network blip) is a
    // routine, expected failure mode for this app - not a bug - so it's translated into a
    // "service unavailable" ApiResult here, once, rather than every Razor Pages handler
    // needing its own try/catch around every call to avoid an unhandled-exception 500 page.
    private static async Task<TResult> GuardAsync<TResult>(
        Func<Task<TResult>> action,
        Func<int, string?, string?, IDictionary<string, string[]>?, TResult> failure)
    {
        try
        {
            return await action();
        }
        catch (HttpRequestException ex)
        {
            return failure(
                StatusCodes.Status503ServiceUnavailable,
                "ProjectA.Api is unreachable",
                $"Could not reach the API. Confirm it is running and that ApiSettings:BaseUrl is correct. ({ex.Message})",
                null);
        }
        catch (TaskCanceledException) when (!Environment.HasShutdownStarted)
        {
            return failure(
                StatusCodes.Status504GatewayTimeout,
                "ProjectA.Api did not respond in time",
                "The request to the API timed out.",
                null);
        }
    }

    private static async Task<ApiResult<T>> ReadResultAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return ApiResult<T>.Success(default, (int)response.StatusCode);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(ApiJson.Options, cancellationToken);
            return ApiResult<T>.Success(value, (int)response.StatusCode);
        }

        var problem = await TryReadProblemAsync(response, cancellationToken);
        return ApiResult<T>.Failure((int)response.StatusCode, problem?.Title, problem?.Detail, problem?.Errors);
    }

    private static async Task<ProblemDetailsPayload?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemDetailsPayload>(ApiJson.Options, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            // No content, or content-type isn't JSON (e.g. the API is unreachable and a
            // proxy/gateway returned an HTML error page instead).
            return null;
        }
    }
}
