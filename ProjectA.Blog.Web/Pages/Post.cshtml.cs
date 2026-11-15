using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Blog.Web.Common;
using ProjectA.Blog.Web.Models;
using ProjectA.Blog.Web.Services;

namespace ProjectA.Blog.Web.Pages;

// Serves both /posts/{slug} (posts) and /{slug} (pages) - Blogifier's two URL shapes. A post
// requested at the page URL (or vice versa) is redirected to its canonical address.
public class PostModel(IBlogApiClient blogApi) : PageModel
{
    public PostDetail Detail { get; private set; } = new();
    public string ContentHtml { get; private set; } = string.Empty;
    public string DefaultCover { get; private set; } = BlogFormat.DefaultCover;
    public string AbsoluteUrl { get; private set; } = string.Empty;
    public string AbsoluteCoverUrl { get; private set; } = string.Empty;

    public bool IsPage => Detail.Post.PostType == PostType.Page;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var siteTask = blogApi.GetSiteAsync(cancellationToken);
        var result = await blogApi.GetPostAsync(slug, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return result.StatusCode == StatusCodes.Status404NotFound
                ? NotFound()
                : StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        Detail = result.Value;

        // This page is reachable at both /posts/{slug} and (via a convention in Program.cs)
        // /{slug}; which one was used decides whether a redirect is needed.
        var requestedAsPage = !Request.Path.StartsWithSegments("/posts");
        if (requestedAsPage != IsPage)
        {
            return RedirectPermanent(BlogFormat.PostPath(Detail.Post.PostType, Detail.Post.Slug));
        }

        // RemoteIpAddress is the reader's address: UseForwardedHeaders replaces nginx's with the
        // X-Forwarded-For value nginx appended.
        await blogApi.RecordViewAsync(
            slug, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), cancellationToken);

        ContentHtml = BlogFormat.MarkdownToHtml(Detail.Content);
        DefaultCover = (await siteTask).Cover ?? BlogFormat.DefaultCover;
        var siteRoot = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        AbsoluteUrl = siteRoot + BlogFormat.PostPath(Detail.Post.PostType, Detail.Post.Slug);
        var cover = BlogFormat.AssetUrl(Detail.Post.Cover, DefaultCover);
        AbsoluteCoverUrl = cover.StartsWith('/') ? siteRoot + cover : cover;
        return Page();
    }
}
