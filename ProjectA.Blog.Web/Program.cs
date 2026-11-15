using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using ProjectA.Blog.Web.Common;
using ProjectA.Blog.Web.Services;

// ProjectA.Blog.Web - the public blog (formerly the Blogifier site at darrentuer.net). It is a
// thin, read-only Razor Pages front end over ProjectA.Api's anonymous /api/blog endpoints;
// writing and managing posts happens in ProjectA.Web's Blog admin pages.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    // Static pages (post type "page", e.g. /about) live at the site root as in Blogifier. Literal
    // routes (/search, /error, /feed/..., /sitemap) still win over this catch-all parameter.
    options.Conventions.AddPageRoute("/Post", "{slug}");
});
builder.Services.AddMemoryCache();
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("feeds", policy => policy.Expire(TimeSpan.FromMinutes(20)));
});

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    throw new InvalidOperationException(
        "ApiSettings:BaseUrl is not configured. Set it in appsettings.json to the ProjectA.Api base address.");
}

builder.Services.AddHttpClient<IBlogApiClient, BlogApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

// nginx terminates TLS and proxies to Kestrel on localhost; without this, Request.Scheme would
// be "http" and every canonical/RSS/share URL would point at http://.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// Unknown URLs render the themed "Page Not Found" page (Blogifier's 404.cshtml).
app.UseStatusCodePagesWithReExecute("/error", "?code={0}");

app.UseStaticFiles();

// Blog uploads: Blogifier's wwwroot/data, now a folder shared with ProjectA.Web's admin
// (BlogStorage:RootPath, on EFS in production). Served at /data so every existing cover and
// in-post image URL ("data/2/626.jpg", "/data/1/2023/12/x.jpg") keeps working unchanged.
var storageRoot = BlogStorage.ResolveRoot(app.Configuration, app.Environment);
Directory.CreateDirectory(storageRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageRoot),
    RequestPath = "/data"
});

app.UseRouting();
app.UseOutputCache();

app.MapRazorPages();

// Blogifier's admin and login lived under /admin on this site; they now live in ProjectA.Web.
// Old bookmarks (and the theme's login icon) are sent there instead of 404ing.
var adminUrl = app.Configuration["BlogSite:AdminUrl"];
app.MapGet("/admin/{**rest}", () => string.IsNullOrWhiteSpace(adminUrl) ? Results.NotFound() : Results.Redirect(adminUrl));

// RSS 2.0 feed at Blogifier's address (/feed/rss) - kept so existing subscribers keep working.
app.MapGet("/feed/{type}", async (HttpContext context, IBlogApiClient api, CancellationToken cancellationToken) =>
{
    var siteTask = api.GetSiteAsync(cancellationToken);
    var entriesResult = await api.GetFeedAsync(cancellationToken);
    var site = await siteTask;
    if (!entriesResult.IsSuccess)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    var host = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
    var entries = entriesResult.Value ?? [];

    var feed = new SyndicationFeed(site.Title, site.Description ?? string.Empty, new Uri(host), host,
        entries.Count > 0 ? entries[0].PublishedAt : DateTimeOffset.UtcNow)
    {
        Items = entries.Select(entry =>
        {
            var url = $"{host}/posts/{entry.Slug}";
            var item = new SyndicationItem(entry.Title, BlogFormat.MarkdownToHtml(entry.Content), new Uri(url), url, entry.PublishedAt)
            {
                PublishDate = entry.PublishedAt
            };
            foreach (var category in entry.Categories)
            {
                item.Categories.Add(new SyndicationCategory(category));
            }

            return item;
        }).ToList()
    };

    using var stream = new MemoryStream();
    await using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = Encoding.UTF8, Indent = true, Async = true }))
    {
        new Rss20FeedFormatter(feed, false).WriteTo(writer);
        await writer.FlushAsync();
    }

    return Results.File(stream.ToArray(), "application/xml; charset=utf-8");
}).CacheOutput("feeds");

app.MapGet("/sitemap", async (HttpContext context, IBlogApiClient api, CancellationToken cancellationToken) =>
{
    var result = await api.GetSitemapAsync(cancellationToken);
    if (!result.IsSuccess)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    var host = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
    XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    var document = new XDocument(
        new XDeclaration("1.0", "utf-8", null),
        new XElement(ns + "urlset",
            (result.Value ?? []).Select(entry => new XElement(ns + "url",
                new XElement(ns + "loc", host + BlogFormat.PostPath(entry.PostType, entry.Slug)),
                new XElement(ns + "lastmod", entry.LastModified.ToString("yyyy-MM-ddTHH:mm:ssK")),
                new XElement(ns + "changefreq", "monthly")))));

    return Results.Text(document.Declaration + Environment.NewLine + document, "text/xml", Encoding.UTF8);
}).CacheOutput("feeds");

app.Run();
