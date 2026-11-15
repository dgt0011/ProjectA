using ProjectA.Blog.Web.Models;

namespace ProjectA.Blog.Web.Pages.Shared;

// What the list partials (_PostList, _Featured, _Pagination) need: the page of posts, how to
// build a link to another page of the same listing, and the cover to use when a post has none.
public sealed class PostListViewModel(PostPage page, Func<int, string> pageLink, string? defaultCover, string emptyMessage = "empty")
{
    public PostPage Page { get; } = page;
    public string? DefaultCover { get; } = string.IsNullOrWhiteSpace(defaultCover) ? Common.BlogFormat.DefaultCover : defaultCover;
    public string EmptyMessage { get; } = emptyMessage;

    public string PageLink(int page) => pageLink(page);
}
