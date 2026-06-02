using System.ComponentModel.DataAnnotations;

namespace ProjectA.Api.ToDo;

public record ToDo(
    uint Id,
    uint CategoryId,
    string Title,
    string Description,
    DateTime DateCreated,
    DateTime DateModified,
    bool Done
    );