using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Blog;

// Blog dashboard - Blogifier's admin home (totals + views of the latest posts).
[Authorize]
public class IndexModel(IBlogAdminApiClient blogAdminApiClient, IConfiguration configuration) : PageModel
{
    public static readonly int[] LatestOptions = [7, 30, 90];

    [BindProperty(SupportsGet = true)]
    public int Latest { get; set; } = 7;

    public BlogAnalyticsDto? Analytics { get; private set; }

    public string? BlogSiteUrl => configuration["BlogSite:BaseUrl"];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!LatestOptions.Contains(Latest))
        {
            Latest = LatestOptions[0];
        }

        var result = await blogAdminApiClient.GetAnalyticsAsync(Latest, cancellationToken);
        if (result.IsSuccess)
        {
            Analytics = result.Value;
        }
        else
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Could not load blog analytics.");
        }
    }
}
