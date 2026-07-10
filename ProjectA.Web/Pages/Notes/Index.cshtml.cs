using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Notes;

public class IndexModel(INotesApiClient notesApiClient) : PageModel
{
    private const int ExcerptLength = 120;

    public List<NoteDto> Notes { get; set; } = [];
    public bool LoadedSuccessfully { get; set; } = true;

    public string Excerpt(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var singleLine = body.ReplaceLineEndings(" ");
        return singleLine.Length <= ExcerptLength ? singleLine : singleLine[..ExcerptLength] + "…";
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await notesApiClient.GetListAsync(cancellationToken);
        if (result.IsSuccess)
        {
            Notes = result.Value ?? [];
        }
        else
        {
            LoadedSuccessfully = false;
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id, CancellationToken cancellationToken)
    {
        var result = await notesApiClient.DeleteAsync(id, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.IsSuccess
            ? "Note deleted."
            : result.ToDisplayMessage("Could not delete the note.");

        return RedirectToPage();
    }
}
