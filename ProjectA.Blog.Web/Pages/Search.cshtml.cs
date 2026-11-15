using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Blog.Web.Models;
using ProjectA.Blog.Web.Pages.Shared;
using ProjectA.Blog.Web.Services;

namespace ProjectA.Blog.Web.Pages;

public class SearchModel(IBlogApiClient blogApi) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Term { get; set; }

    public PostListViewModel List { get; private set; } = null!;

    public async Task OnGetAsync(int? page, CancellationToken cancellationToken)
    {
        var pageNumber = page is > 0 ? page.Value : 1;
        var site = await blogApi.GetSiteAsync(cancellationToken);
        var results = PostPage.Empty;

        if (!string.IsNullOrWhiteSpace(Term))
        {
            var result = await blogApi.SearchAsync(Term.Trim(), pageNumber, cancellationToken);
            results = result.Value ?? PostPage.Empty;
        }

        var encodedTerm = Uri.EscapeDataString(Term ?? string.Empty);
        List = new PostListViewModel(results, p => $"?term={encodedTerm}&page={p}", site.Cover, "no results");
    }
}
