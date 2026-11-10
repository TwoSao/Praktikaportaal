using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Praktikaportaal.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Login: cookie holding the user's data and the API's JWT ─────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Praktikaportaal.Auth";
        options.Cookie.HttpOnly = true;             // the JWT inside is never readable by page scripts
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(24); // replaced at login by the JWT's own expiry
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();

// ── The only data source: the REST API ──────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerTokenHandler>();

string apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5117";
builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    })
    .AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<ApiExceptionFilter>();
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()); // every POST needs the form token
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

/// <summary>Makes the entry point visible to WebApplicationFactory in tests.</summary>
public partial class Program;
