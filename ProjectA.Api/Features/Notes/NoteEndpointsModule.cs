using ProjectA.Api.Features.Notes.CreateNote;
using ProjectA.Api.Features.Notes.DeleteNote;
using ProjectA.Api.Features.Notes.GetNoteById;
using ProjectA.Api.Features.Notes.GetNoteList;
using ProjectA.Api.Features.Notes.UpdateNote;

namespace ProjectA.Api.Features.Notes;

public static class NoteEndpointsModule
{
    public static void MapNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notes")
            .WithTags("Notes");

        group.MapGetNoteList();
        group.MapGetNoteById();
        group.MapCreateNote();
        group.MapUpdateNote();
        group.MapDeleteNote();
    }
}
