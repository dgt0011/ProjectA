using ProjectA.Api.Features.Categories.CreateCategory;
using ProjectA.Api.Features.Categories.DeleteCategory;
using ProjectA.Api.Features.Categories.GetCategoryById;
using ProjectA.Api.Features.Categories.GetCategoryList;
using ProjectA.Api.Features.Categories.UpdateCategory;

namespace ProjectA.Api.Features.Categories;

public static class CategoryEndpointsModule
{
    public static void MapCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/categories")
            .WithTags("Categories");

        group.MapGetCategoryList();
        group.MapGetCategoryById();
        group.MapCreateCategory();
        group.MapUpdateCategory();
        group.MapDeleteCategory();
    }
}
