using ProjectA.Api.Features.Attachments.CreateAttachment;
using ProjectA.Api.Features.Attachments.DeleteAttachment;
using ProjectA.Api.Features.Attachments.GetAttachmentById;
using ProjectA.Api.Features.Attachments.GetAttachmentList;
using ProjectA.Api.Features.Attachments.UpdateAttachment;

namespace ProjectA.Api.Features.Attachments;

public static class AttachmentEndpointsModule
{
    public static void MapAttachmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/attachments")
            .WithTags("Attachments");

        group.MapGetAttachmentList();
        group.MapGetAttachmentById();
        group.MapCreateAttachment();
        group.MapUpdateAttachment();
        group.MapDeleteAttachment();
    }
}
