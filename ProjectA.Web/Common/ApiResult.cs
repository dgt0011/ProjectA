namespace ProjectA.Web.Common;

// Result of a call that returns a body (GET/POST/PUT). Never throws for "expected" HTTP
// failures (400/404/409/etc.) - callers branch on IsSuccess instead of catching exceptions,
// keeping Razor Pages handlers free of try/catch around every API call.
public sealed class ApiResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public int StatusCode { get; }
    public string? Title { get; }
    public string? Detail { get; }
    public IDictionary<string, string[]>? Errors { get; }

    private ApiResult(bool isSuccess, T? value, int statusCode, string? title, string? detail, IDictionary<string, string[]>? errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
        Errors = errors;
    }

    public static ApiResult<T> Success(T? value, int statusCode) => new(true, value, statusCode, null, null, null);

    public static ApiResult<T> Failure(int statusCode, string? title, string? detail, IDictionary<string, string[]>? errors) =>
        new(false, default, statusCode, title, detail, errors);

    // Convenience message for callers that just want something to show the user, regardless
    // of whether the API sent a ValidationProblem (Errors) or a plain Problem (Detail/Title).
    public string ToDisplayMessage(string fallback) =>
        Detail ?? Title ?? (Errors is { Count: > 0 } ? string.Join(" ", Errors.SelectMany(e => e.Value)) : fallback);
}

// Non-generic counterpart for calls with no response body, i.e. DELETE.
public sealed class ApiResult
{
    public bool IsSuccess { get; }
    public int StatusCode { get; }
    public string? Title { get; }
    public string? Detail { get; }
    public IDictionary<string, string[]>? Errors { get; }

    private ApiResult(bool isSuccess, int statusCode, string? title, string? detail, IDictionary<string, string[]>? errors)
    {
        IsSuccess = isSuccess;
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
        Errors = errors;
    }

    public static ApiResult Success(int statusCode) => new(true, statusCode, null, null, null);

    public static ApiResult Failure(int statusCode, string? title, string? detail, IDictionary<string, string[]>? errors) =>
        new(false, statusCode, title, detail, errors);

    public string ToDisplayMessage(string fallback) =>
        Detail ?? Title ?? (Errors is { Count: > 0 } ? string.Join(" ", Errors.SelectMany(e => e.Value)) : fallback);
}
