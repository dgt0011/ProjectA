using ProjectA.Api.Features.Blog.Analytics;
using ProjectA.Api.Features.Blog.Categories;
using ProjectA.Api.Features.Blog.Posts.CreateBlogPost;
using ProjectA.Api.Features.Blog.Posts.DeleteBlogPost;
using ProjectA.Api.Features.Blog.Posts.GetBlogPostById;
using ProjectA.Api.Features.Blog.Posts.GetBlogPostList;
using ProjectA.Api.Features.Blog.Posts.SetBlogPostState;
using ProjectA.Api.Features.Blog.Posts.UpdateBlogPost;
using ProjectA.Api.Features.Blog.Public.GetBlogSite;
using ProjectA.Api.Features.Blog.Public.GetFeedEntries;
using ProjectA.Api.Features.Blog.Public.GetPublishedPostBySlug;
using ProjectA.Api.Features.Blog.Public.GetPublishedPostList;
using ProjectA.Api.Features.Blog.Public.RecordPostView;
using ProjectA.Api.Features.Blog.Public.SearchPublishedPosts;
using ProjectA.Api.Features.Blog.Settings;

namespace ProjectA.Api.Features.Blog;

// The blog (migrated from Blogifier) is split into two audiences:
//   /api/blog/...            - anonymous, read-only, published content only. Used by
//                              ProjectA.Blog.Web to render the public site.
//   /api/blog/posts|categories|settings|analytics
//                            - authenticated admin endpoints used by ProjectA.Web's Blog pages.
public static class BlogEndpointsModule
{
    public static void MapBlogEndpoints(this WebApplication app)
    {
        var publicGroup = app.MapGroup("/api/blog")
            .WithTags("Blog (public)")
            .AllowAnonymous();

        publicGroup.MapGetBlogSite();
        publicGroup.MapGetPublishedPostList();
        publicGroup.MapGetPublishedPostBySlug();
        publicGroup.MapSearchPublishedPosts();
        publicGroup.MapRecordPostView();
        publicGroup.MapGetFeedEntries();

        var posts = app.MapGroup("/api/blog/posts")
            .WithTags("Blog posts")
            .RequireAuthorization();

        posts.MapGetBlogPostList();
        posts.MapGetBlogPostById();
        posts.MapCreateBlogPost();
        posts.MapUpdateBlogPost();
        posts.MapDeleteBlogPost();
        posts.MapSetBlogPostState();

        app.MapGroup("/api/blog/categories")
            .WithTags("Blog categories")
            .RequireAuthorization()
            .MapBlogCategoryEndpoints();

        app.MapGroup("/api/blog/settings")
            .WithTags("Blog settings")
            .RequireAuthorization()
            .MapBlogSettingsEndpoints();

        app.MapGroup("/api/blog/analytics")
            .WithTags("Blog analytics")
            .RequireAuthorization()
            .MapGetBlogAnalytics();
    }
}
