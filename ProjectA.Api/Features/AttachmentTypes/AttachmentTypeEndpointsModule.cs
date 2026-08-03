using ProjectA.Api.Features.AttachmentTypes.CreateAttachmentType;
using ProjectA.Api.Features.AttachmentTypes.DeleteAttachmentType;
using ProjectA.Api.Features.AttachmentTypes.GetAttachmentTypeById;
using ProjectA.Api.Features.AttachmentTypes.GetAttachmentTypeIcon;
using ProjectA.Api.Features.AttachmentTypes.GetAttachmentTypeList;
using ProjectA.Api.Features.AttachmentTypes.UpdateAttachmentType;

namespace ProjectA.Api.Features.AttachmentTypes;

public static class AttachmentTypeEndpointsModule
{
    public static void MapAttachmentTypeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/attachmenttypes")
            .WithTags("AttachmentTypes");

        group.MapGetAttachmentTypeList();
        group.MapGetAttachmentTypeById();
        group.MapGetAttachmentTypeIcon();
        group.MapCreateAttachmentType();
        group.MapUpdateAttachmentType();
        group.MapDeleteAttachmentType();
    }
}
