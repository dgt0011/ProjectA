using ProjectA.Api.Features.Projects.CreateProject;
using ProjectA.Api.Features.Projects.DeleteProject;
using ProjectA.Api.Features.Projects.GetProjectById;
using ProjectA.Api.Features.Projects.GetProjectList;
using ProjectA.Api.Features.Projects.UpdateProject;

namespace ProjectA.Api.Features.Projects;

public static class ProjectEndpointsModule
{
    public static void MapProjectEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/projects")
            .WithTags("Projects");

        group.MapGetProjectList();
        group.MapGetProjectById();
        group.MapCreateProject();
        group.MapUpdateProject();
        group.MapDeleteProject();
    }
}
