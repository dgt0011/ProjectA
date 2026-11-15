using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Blog.Web.Models;
using ProjectA.Blog.Web.Pages.Shared;
using ProjectA.Blog.Web.Services;

namespace ProjectA.Blog.Web.Pages;

public class CategoryModel(IBlogApiClient blogApi) : PageModel
{
    public string Category { get; private set; } = string.Empty;
    public PostListViewModel List { get; private set; } = null!;

    public async Task OnGetAsync(string category, int? page, CancellationToken cancellationToken)
    {
        Category = category;
        var pageNumber = page is > 0 ? page.Value : 1;
        var siteTask = blogApi.GetSiteAsync(cancellationToken);
        var result = await blogApi.GetPublishedAsync(pageNumber, category, cancellationToken);
        var site = await siteTask;

        if (!result.IsSuccess)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }

        List = new PostListViewModel(result.Value ?? PostPage.Empty, p => $"?page={p}", site.Cover);
    }
}
