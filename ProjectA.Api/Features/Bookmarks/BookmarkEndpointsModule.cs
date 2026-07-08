using ProjectA.Api.Features.Bookmarks.CreateBookmark;
using ProjectA.Api.Features.Bookmarks.DeleteBookmark;
using ProjectA.Api.Features.Bookmarks.GetBookmarkById;
using ProjectA.Api.Features.Bookmarks.GetBookmarkList;
using ProjectA.Api.Features.Bookmarks.UpdateBookmark;

namespace ProjectA.Api.Features.Bookmarks;

public static class BookmarkEndpointsModule
{
    public static void MapBookmarkEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/bookmarks")
            .WithTags("Bookmarks");

        group.MapGetBookmarkList();
        group.MapGetBookmarkById();
        group.MapCreateBookmark();
        group.MapUpdateBookmark();
        group.MapDeleteBookmark();
    }
}
