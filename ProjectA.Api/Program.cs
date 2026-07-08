using ProjectA.Api.Data;
using ProjectA.Api.Features.Attachments;
using ProjectA.Api.Features.Bookmarks;
using ProjectA.Api.Features.Categories;
using ProjectA.Api.Features.Notes;
using ProjectA.Api.Features.Projects;
using ProjectA.Api.Features.ToDo;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";

        if (context.Exception != null)
        {
      
        }
    };
});
//builder.Services.AddHealthChecks()
//   .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
//    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

builder.Services.AddSingleton<IDbConnectionFactory>(_ =>
    new PostgresDbConnectionFactory(builder.Configuration.GetConnectionString("DefaultConnection")!));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

//app.MapHealthChecks("/health", new HealthCheckOptions
//    {
//        Predicate = registration => registration.Tags.Contains("live")
 //   })
//    .ExcludeFromDescription();

//app.MapHealthChecks("/ready", new HealthCheckOptions
//   {
//        Predicate = registration => registration.Tags.Contains("ready")
//    })
//    .ExcludeFromDescription();

app.MapToDoEndpoints();
app.MapCategoryEndpoints();
<<<<<<< HEAD
app.MapBookmarkEndpoints();
app.MapNoteEndpoints();
app.MapAttachmentEndpoints();
app.MapProjectEndpoints();
=======
>>>>>>> origin/main

app.Run();

public partial class Program;
