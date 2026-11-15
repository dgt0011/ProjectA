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
builder.Services.AddSingleton<ViewRecordingPolicy>();
builder.Services.AddOutputCache(options =>
{
    // Query strings don't change the feed or sitemap, so they aren't part of the cache key -
    // otherwise /feed/rss?x=<random> would miss the cache (and hit the API) on every request.
    options.AddPolicy("feeds", policy => policy.Expire(TimeSpan.FromMinutes(20)).SetVaryByQuery(Array.Empty<string>()));
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
// be "http" and every canonical/RSS/share URL would point at http://, and the reader's IP
// (recorded with post views) would be nginx's.
// - Only the loopback proxy (the defaults in KnownProxies/KnownIPNetworks) is trusted.
// - X-Forwarded-Host is deliberately NOT honoured: nginx already passes the real host in Host,
//   and passes a client-supplied X-Forwarded-Host through untouched, which would let anyone
//   choose the host used in canonical, feed and sitemap links.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();

// Baseline security headers on every response (pages, feed, static files). No site-wide CSP:
// the admin-managed header/footer scripts and raw HTML in posts are allowed by design, so a
// policy would have to permit inline script anyway. Uploads under /data get a strict one below.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

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
    RequestPath = "/data",
    // Uploaded files are only ever images/documents to display or download; if one were ever
    // rendered as a document (a polyglot, or a misnamed upload), it can't run script or reach
    // anything.
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.ContentSecurityPolicy = BlogStorage.UploadContentSecurityPolicy
});

app.UseRouting();
app.UseOutputCache();

app.MapRazorPages();

// Blogifier's admin and login lived under /admin on this site; they now live in ProjectA.Web.
// Old bookmarks (and the theme's login icon) are sent there instead of 404ing.
var adminUrl = app.Configuration["BlogSite:AdminUrl"];
app.MapGet("/admin/{**rest}", () => string.IsNullOrWhiteSpace(adminUrl) ? Results.NotFound() : Results.Redirect(adminUrl));

// RSS 2.0 feed at Blogifier's address (/feed/rss) - kept so existing subscribers keep working.
// Only "rss" exists; any other {type} is a 404 rather than another separately cached copy.
app.MapGet("/feed/{type:regex(^rss$)}", async (HttpContext context, IBlogApiClient api, CancellationToken cancellationToken) =>
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
            // Feed readers don't resolve site-relative links ("/data/...", "data/...") against
            // the site, so they are made absolute.
            var body = BlogFormat.AbsolutizeUrls(BlogFormat.MarkdownToHtml(entry.Content), host);
            var item = new SyndicationItem(entry.Title, body, new Uri(url), url, entry.PublishedAt)
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
