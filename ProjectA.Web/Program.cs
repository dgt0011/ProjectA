using Microsoft.AspNetCore.Authentication.Cookies;
using ProjectA.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// BearerTokenHandler needs the current request's HttpContext (to read the signed-in user's
// api_token claim) even though it runs inside HttpClient's pipeline, not a controller/page.
builder.Services.AddHttpContextAccessor();

// This is the Web app's own browser session - separate from the bearer token it holds for
// calling ProjectA.Api (see BearerTokenHandler). Unauthenticated visitors get redirected to
// LoginPath by [Authorize] on the Create/Edit page models.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddTransient<BearerTokenHandler>();

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    throw new InvalidOperationException(
        "ApiSettings:BaseUrl is not configured. Set it in appsettings.json to the ProjectA.Api base address.");
}

// Anonymous - this is how a token is obtained in the first place, so it never goes through
// BearerTokenHandler.
builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

// One typed HttpClient per entity, all pointed at the same ProjectA.Api instance. Keeping
// them separate (rather than a single shared client) mirrors the API's own vertical-slice
// layout and keeps each Razor Pages section only depending on the client it actually needs.
// Each one gets BearerTokenHandler so Create/Update/Delete calls carry the signed-in user's
// token.
builder.Services.AddHttpClient<ICategoriesApiClient, CategoriesApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IBookmarksApiClient, BookmarksApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IAttachmentsApiClient, AttachmentsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IToDoApiClient, ToDoApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<INotesApiClient, NotesApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IProjectsApiClient, ProjectsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IUsersApiClient, UsersApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
}).AddHttpMessageHandler<BearerTokenHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
