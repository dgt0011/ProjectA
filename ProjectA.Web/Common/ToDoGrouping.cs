using ProjectA.Web.Models;

namespace ProjectA.Web.Common;

// Shared by ToDo/Index and Projects/Details: both pages list ToDo items grouped by category,
// with uncategorized items shown first (as their own "Uncategorized" group) and the remaining
// items grouped under their category, categories ordered alphabetically by title. Used instead
// of a flat list now that CategoryId is optional on a ToDo.
public static class ToDoGrouping
{
    public sealed record ToDoGroup(string? CategoryTitle, List<ToDoDto> Items)
    {
        public bool IsUncategorized => CategoryTitle is null;
    }

    // Grouping/ordering only - relative order of items already present within the source
    // sequence (e.g. outstanding-then-completed, oldest-first) is preserved within each group,
    // since GroupBy/OrderBy are both stable with respect to source order.
    public static List<ToDoGroup> GroupByCategory(IEnumerable<ToDoDto> todos, IReadOnlyDictionary<long, string> categoryTitlesById)
    {
        var groups = new List<ToDoGroup>();

        var uncategorized = todos.Where(todo => todo.CategoryId is null).ToList();
        if (uncategorized.Count > 0)
        {
            groups.Add(new ToDoGroup(null, uncategorized));
        }

        var categorized = todos
            .Where(todo => todo.CategoryId is not null)
            .GroupBy(todo => categoryTitlesById.GetValueOrDefault(todo.CategoryId!.Value, $"#{todo.CategoryId}"))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ToDoGroup(group.Key, group.ToList()));

        groups.AddRange(categorized);

        return groups;
    }

    // ToDo/Index's "Show Project related items" section: Project-associated ToDos grouped by
    // Project (ordered alphabetically by Project title), each Project's own items then grouped
    // the same Uncategorized-first/alphabetical-by-category way as GroupByCategory.
    public sealed record ProjectToDoGroup(string ProjectTitle, List<ToDoGroup> CategoryGroups);

    public static List<ProjectToDoGroup> GroupByProjectThenCategory(
        IEnumerable<ToDoDto> todos,
        IReadOnlyDictionary<long, string> categoryTitlesById,
        IReadOnlyDictionary<long, string> projectTitlesById)
    {
        return todos
            .Where(todo => todo.ProjectId is not null)
            .GroupBy(todo => projectTitlesById.GetValueOrDefault(todo.ProjectId!.Value, $"#{todo.ProjectId}"))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProjectToDoGroup(group.Key, GroupByCategory(group, categoryTitlesById)))
            .ToList();
    }
}
