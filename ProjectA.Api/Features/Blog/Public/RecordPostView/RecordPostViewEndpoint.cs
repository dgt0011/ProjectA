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

    // Repeat views of one post by the same reader inside this window are not counted again.
    public const int RepeatViewWindowMinutes = 30;

    public static void MapRecordPostView(this RouteGroupBuilder group)
    {
        group.MapPost("published/{slug}/views", Handle)
            .WithName("RecordPostView")
            .WithSummary("Count one view of a published post")
            .WithDescription(
                "Called by the public blog each time a post is displayed - feeds the view counts and " +
                "daily-views charts on the admin dashboard. Increments the post's all-time counter and " +
                "logs the view with the reader's IP address and user agent (both optional; an IP that " +
                "doesn't parse is dropped). A repeat view of the same post by the same IP address + " +
                "user agent within 30 minutes is accepted but not counted. Anonymous by design " +
                "(readers aren't signed in); only ever adds views, and only for published posts.");
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
            userAgent = Truncate(userAgent);
        }

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        var postId = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            $"SELECT p.id FROM blog_posts p WHERE p.slug = @Slug AND {PublishedPosts.VisibleFilter};",
            new { Slug = slug },
            cancellationToken: cancellationToken));
        if (postId is null)
        {
            return TypedResults.NotFound();
        }

        // A reader (same IP address + user agent) re-viewing the same post within the window -
        // refreshes, back/forward, a scripted loop - counts once. Uses the (post_id, viewed_at)
        // index, so the check only scans this post's recent views.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            WITH fresh AS (
                SELECT @PostId AS id
                WHERE NOT EXISTS (
                    SELECT 1 FROM blog_post_views v
                    WHERE v.post_id = @PostId
                      AND v.viewed_at > now() - make_interval(mins => @WindowMinutes)
                      AND v.ip_address IS NOT DISTINCT FROM CAST(@IpAddress AS inet)
                      AND v.user_agent IS NOT DISTINCT FROM CAST(@UserAgent AS varchar))
            ),
            counted AS (
                UPDATE blog_posts p SET post_views = p.post_views + 1
                FROM fresh WHERE p.id = fresh.id
                RETURNING p.id
            )
            INSERT INTO blog_post_views (post_id, ip_address, user_agent)
            SELECT id, CAST(@IpAddress AS inet), @UserAgent FROM counted;
            """,
            new { PostId = postId.Value, WindowMinutes = RepeatViewWindowMinutes, IpAddress = ipAddress, UserAgent = userAgent },
            cancellationToken: cancellationToken));

        return TypedResults.NoContent();
    }

    // Cuts at MaxUserAgentLength without leaving half of a surrogate pair (which Npgsql refuses
    // to encode).
    private static string Truncate(string value)
    {
        var length = MaxUserAgentLength;
        if (char.IsHighSurrogate(value[length - 1]))
        {
            length--;
        }

        return value[..length];
    }
}
