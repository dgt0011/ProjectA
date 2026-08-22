using Dapper.Contrib.Extensions;

namespace ProjectA.Api.Features.ToDo;

// ReSharper disable InconsistentNaming
[Table("todos")]
internal sealed class ToDoDto
{
    [Dapper.Contrib.Extensions.Key]
    public long id { get; init; }
    public string title { get; set; } = string.Empty;
    public bool actioned { get; set; }
    public long? category_id { get; set; }
    public long? project_id { get; set; }
    public string? description { get; set; }
    public string? completion_notes { get; set; }
    public DateTime? date_created { get; init; }
    public DateTime? date_modified { get; set; }
}
// ReSharper restore InconsistentNaming
