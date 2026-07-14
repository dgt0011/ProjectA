using ProjectA.Api.Features.BookmarkTypes.CreateBookmarkType;
using ProjectA.Api.Features.BookmarkTypes.DeleteBookmarkType;
using ProjectA.Api.Features.BookmarkTypes.GetBookmarkTypeById;
using ProjectA.Api.Features.BookmarkTypes.GetBookmarkTypeIcon;
using ProjectA.Api.Features.BookmarkTypes.GetBookmarkTypeList;
using ProjectA.Api.Features.BookmarkTypes.UpdateBookmarkType;

namespace ProjectA.Api.Features.BookmarkTypes;

public static class BookmarkTypeEndpointsModule
{
    public static void MapBookmarkTypeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/bookmarktypes")
            .WithTags("BookmarkTypes");

        group.MapGetBookmarkTypeList();
        group.MapGetBookmarkTypeById();
        group.MapGetBookmarkTypeIcon();
        group.MapCreateBookmarkType();
        group.MapUpdateBookmarkType();
        group.MapDeleteBookmarkType();
    }
}
