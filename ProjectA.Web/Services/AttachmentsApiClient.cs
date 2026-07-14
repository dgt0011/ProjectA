using ProjectA.Web.Common;
using ProjectA.Web.Models;

namespace ProjectA.Web.Services;

public interface IAttachmentsApiClient
{
    Task<ApiResult<List<AttachmentDto>>> GetListAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<AttachmentDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResult<AttachmentDto>> CreateAsync(AttachmentInput input, CancellationToken cancellationToken = default);
    Task<ApiResult<AttachmentDto>> UpdateAsync(long id, AttachmentInput input, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class AttachmentsApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IAttachmentsApiClient
{
    private const string BasePath = "api/attachments";

    public Task<ApiResult<List<AttachmentDto>>> GetListAsync(CancellationToken cancellationToken = default) =>
        GetJsonAsync<List<AttachmentDto>>(BasePath, cancellationToken);

    public Task<ApiResult<AttachmentDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        GetJsonAsync<AttachmentDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ApiResult<AttachmentDto>> CreateAsync(AttachmentInput input, CancellationToken cancellationToken = default) =>
        PostJsonAsync<AttachmentInput, AttachmentDto>(BasePath, input, cancellationToken);

    public Task<ApiResult<AttachmentDto>> UpdateAsync(long id, AttachmentInput input, CancellationToken cancellationToken = default) =>
        PutJsonAsync<AttachmentInput, AttachmentDto>($"{BasePath}/{id}", input, cancellationToken);

    public Task<ApiResult> DeleteAsync(long id, CancellationToken cancellationToken = default) =>
        DeleteResourceAsync($"{BasePath}/{id}", cancellationToken);
}
