using System.Security.Claims;

namespace ProjectA.Api.Features.Blog;

internal static class BlogCaller
{
    // The "uid" claim JwtTokenService puts on every token - the signed-in ProjectA user, who
    // becomes the author of anything they create on the blog.
    public static long? GetUserId(ClaimsPrincipal caller) =>
        long.TryParse(caller.FindFirst("uid")?.Value, out var id) ? id : null;
}
