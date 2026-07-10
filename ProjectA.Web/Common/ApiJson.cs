using System.Text.Json;

namespace ProjectA.Web.Common;

// Shared serializer options for all calls into ProjectA.Api. JsonSerializerDefaults.Web
// gives camelCase property names on the wire and case-insensitive reads, matching the
// default Microsoft.AspNetCore.Http.Json.JsonOptions the API's minimal API endpoints use.
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
