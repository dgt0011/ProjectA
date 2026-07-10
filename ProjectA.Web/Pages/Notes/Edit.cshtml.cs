using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectA.Web.Common;
using ProjectA.Web.Models;
using ProjectA.Web.Services;

namespace ProjectA.Web.Pages.Notes;

public class EditModel(INotesApiClient notesApiClient) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    [BindProperty]
    public NoteInput Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await notesApiClient.GetByIdAsync(Id, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            TempData["ErrorMessage"] = result.ToDisplayMessage("Note not found.");
            return RedirectToPage("Index");
        }

        Form = new NoteInput { Title = result.Value.Title, Body = result.Value.Body };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await notesApiClient.UpdateAsync(Id, Form, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddApiErrors(result.Errors);
            if (result.Errors is null)
            {
                ModelState.AddModelError(string.Empty, result.ToDisplayMessage("Could not update the note."));
            }

            return Page();
        }

        TempData["SuccessMessage"] = "Note updated.";
        return RedirectToPage("Index");
    }
}
