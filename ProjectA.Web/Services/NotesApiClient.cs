using ProjectA.Web.Common;
using ProjectA.Web.Models;

namespace ProjectA.Web.Services;

public interface INotesApiClient
{
    Task<ApiResult<List<NoteDto>>> GetListAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<NoteDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResult<NoteDto>> CreateAsync(NoteInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<NoteDto>> UpdateAsync(long id, NoteInput input, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class NotesApiClient(HttpClient httpClient) : ApiClientBase(httpClient), INotesApiClient
{
    private const string BasePath = "api/notes";

    public Task<ApiResult<List<NoteDto>>> GetListAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<NoteDto>>(BasePath, cancellationToken);

    public Task<ApiResult<NoteDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        GetJsonAsync<NoteDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<NoteDto>> CreateAsync(NoteInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<NoteInput, NoteDto>(BasePath, input, cancellationToken);

    public Task<ApiResult<NoteDto>> UpdateAsync(long id, NoteInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<NoteInput, NoteDto>($"{BasePath}/{id}", input, cancellationToken);

    public Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        DeleteResourceAsync($"{BasePath}/{id}", cancellationToken);
}
