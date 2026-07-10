using ProjectA.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    throw new InvalidOperationException(
        "ApiSettings:BaseUrl is not configured. Set it in appsettings.json to the ProjectA.Api base address.");
}

// One typed HttpClient per entity, all pointed at the same ProjectA.Api instance. Keeping
// them separate (rather than a single shared client) mirrors the API's own vertical-slice
// layout and keeps each Razor Pages section only depending on the client it actually needs.
builder.Services.AddHttpClient<ICategoriesApiClient, CategoriesApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddHttpClient<IBookmarksApiClient, BookmarksApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddHttpClient<IAttachmentsApiClient, AttachmentsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddHttpClient<IToDoApiClient, ToDoApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddHttpClient<INotesApiClient, NotesApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddHttpClient<IProjectsApiClient, ProjectsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
