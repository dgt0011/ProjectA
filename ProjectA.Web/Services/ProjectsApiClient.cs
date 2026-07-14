using ProjectA.Web.Common;
using ProjectA.Web.Models;

namespace ProjectA.Web.Services;

public interface IProjectsApiClient
{
    Task<ApiResult<List<ProjectDto>>> GetListAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<ProjectDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResult<ProjectDto>> CreateAsync(ProjectInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<ProjectDto>> UpdateAsync(long id, ProjectInput input, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ProjectsApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IProjectsApiClient
{
    private const string BasePath = "api/projects";

    public Task<ApiResult<List<ProjectDto>>> GetListAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<ProjectDto>>(BasePath, cancellationToken);

    public Task<ApiResult<ProjectDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        GetJsonAsync<ProjectDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<ProjectDto>> CreateAsync(ProjectInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<ProjectWireInput, ProjectDto>(BasePath, ToWireInput(input), cancellationToken);

    public Task<ApiResult<ProjectDto>> UpdateAsync(long id, ProjectInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<ProjectWireInput, ProjectDto>($"{BasePath}/{id}", ToWireInput(input), cancellationToken);

    public Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        DeleteResourceAsync($"{BasePath}/{id}", cancellationToken);

    // ProjectA.Api's project rows use a `timestamptz` column, and Npgsql requires DateTime
    // values written to timestamptz to have Kind = Utc - so the DateOnly the form collects is
    // converted to a UTC midnight DateTime here, at the API boundary, rather than asking the
    // Razor Pages layer to know about that requirement.
    private static ProjectWireInput ToWireInput(ProjectInput input) => new(
        input.Title,
        input.Description,
        input.StartDate is { } startDate
            ? DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
            : null,
        input.NoteIds,
        input.BookmarkIds,
        input.AttachmentIds);

    private sealed record ProjectWireInput(
        string Title,
        string? Description,
        DateTime? StartDate,
        List<long> NoteIds,
        List<long> BookmarkIds,
        List<long> AttachmentIds);
}
