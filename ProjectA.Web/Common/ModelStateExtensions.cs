using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ProjectA.Web.Common;

public static class ModelStateExtensions
{
    // Maps ProjectA.Api's ValidationProblem errors (keyed by request property name, e.g.
    // "Title", "CategoryIds") onto ModelState entries for the bound "Form.<Property>" fields
    // every Create/Edit page uses, so asp-validation-for can surface them inline.
    public static void AddApiErrors(this ModelStateDictionary modelState, IDictionary<string, string[]>? errors, string prefix = "Form")
    {
        if (errors is null)
        {
            return;
        }

        foreach (var (key, messages) in errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError($"{prefix}.{key}", message);
            }
        }
    }
}
