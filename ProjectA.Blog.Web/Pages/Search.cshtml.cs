using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Blog.Web.Models;
using ProjectA.Blog.Web.Pages.Shared;
using ProjectA.Blog.Web.Services;

namespace ProjectA.Blog.Web.Pages;

public class SearchModel(IBlogApiClient blogApi) : PageModel
{
    private const int MaxTermLength = 200;

    [BindProperty(SupportsGet = true)]
    public string? Term { get; set; }

    public PostListViewModel List { get; private set; } = null!;

    public async Task OnGetAsync(int? page, CancellationToken cancellationToken)
    {
        var pageNumber = page is > 0 ? page.Value : 1;
        var site = await blogApi.GetSiteAsync(cancellationToken);

        // The API only uses the first 200 characters (and first 10 words) anyway; trimming here
        // keeps oversized terms out of the API call and the pagination links.
        if (Term is { Length: > MaxTermLength })
        {
            Term = Term[..MaxTermLength];
        }
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
