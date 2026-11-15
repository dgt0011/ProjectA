using System.Net;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using ProjectA.Api.Data;

namespace ProjectA.Api.Features.Blog.Public.RecordPostView;

// The reader's details as seen by ProjectA.Blog.Web (the API only ever sees Blog.Web's own
// address, so Blog.Web passes them on). Both optional.
public sealed record RecordPostViewRequest(string? IpAddress, string? UserAgent);

public static class RecordPostViewEndpoint
{
    public const int MaxUserAgentLength = 512;

    public static void MapRecordPostView(this RouteGroupBuilder group)
    {
        group.MapPost("published/{slug}/views", Handle)
            .WithName("RecordPostView")
            .WithSummary("Count one view of a published post")
            .WithDescription(
                "Called by the public blog each time a post is displayed - feeds the view counts and " +
                "daily-views charts on the admin dashboard. Increments the post's all-time counter and " +
                "logs the view with the reader's IP address and user agent (both optional; an IP that " +
                "doesn't parse is dropped). Anonymous by design (readers aren't signed in); only ever " +
                "adds views, and only for published posts.");
    }

    private static async Task<Results<NoContent, NotFound>> Handle(
        string slug,
        RecordPostViewRequest? request,
        IDbConnectionFactory connectionFactory,
        CancellationToken cancellationToken = default)
    {
        var ipAddress = IPAddress.TryParse(request?.IpAddress?.Trim(), out var parsed)
            ? (parsed.IsIPv4MappedToIPv6 ? parsed.MapToIPv4() : parsed).ToString()
            : null;
        var userAgent = request?.UserAgent?.Trim();
        if (string.IsNullOrEmpty(userAgent))
        {
            userAgent = null;
        }
        else if (userAgent.Length > MaxUserAgentLength)
        {
            userAgent = userAgent[..MaxUserAgentLength];
        }

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            $"""
            WITH viewed AS (
                UPDATE blog_posts p SET post_views = p.post_views + 1
                WHERE p.slug = @Slug AND {PublishedPosts.VisibleFilter}
                RETURNING p.id
            )
            INSERT INTO blog_post_views (post_id, ip_address, user_agent)
            SELECT id, CAST(@IpAddress AS inet), @UserAgent FROM viewed;
            """,
            new { Slug = slug, IpAddress = ipAddress, UserAgent = userAgent },
            cancellationToken: cancellationToken));

        return inserted > 0 ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
