using Backend.Components;
using Backend.Data;
using Backend.Features.Auth;
using Backend.Features.Sites;
using Backend.Services;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

// ponytail: .env -> env vars in a few lines. Swap in DotNetEnv if we ever need
// quoting, escapes or multi-line values. Must run before CreateBuilder so the
// default environment-variable config provider picks these up.
// The file lives in the solution root; CWD is this project's directory under
// `dotnet run`/AppHost, and in Docker there is no file at all — compose passes
// the same keys as real environment variables.
var envFile = new[] { ".env", "../.env" }.FirstOrDefault(File.Exists);

foreach (var line in envFile is null ? [] : File.ReadAllLines(envFile))
{
    var trimmed = line.Trim();
    if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
    var split = trimmed.Split('=', 2);
    // A real environment variable wins — .env is the local-dev fallback, not an
    // override of whatever the host set.
    if (split.Length == 2 && Environment.GetEnvironmentVariable(split[0].Trim()) is null)
        Environment.SetEnvironmentVariable(split[0].Trim(), split[1].Trim());
}

var builder = WebApplication.CreateBuilder(args);

// Scans the assembly at startup for every Endpoint/Validator class and registers
// them. There is no route table to maintain — the endpoint classes are the routes.
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument();

// The admin console. Interactive server rendering: the components run in this
// process, so they call the services below directly — no HTTP, no serialisation,
// no second deployable.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("comments")));

// The rules live here, not in the endpoints or the components. Both go through a
// service; what is allowed is decided once.
builder.Services.AddScoped<SiteService>();
builder.Services.AddScoped<CommentService>();
builder.Services.AddScoped<AccountService>();

// Blogs are static sites on other origins, so the reader's session cookie is
// cross-site: SameSite=None + Secure, and CORS must allow credentials. The
// allowed origins are the registered sites, resolved per request — assigned
// after Build() below because the policy is declared before the provider exists.
// The console needs no CORS entry at all now: it is served from this origin.
IServiceProvider? services = null;

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .SetIsOriginAllowed(origin => SiteOrigins.IsAllowed(services!, origin))
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// Missing credentials must not take the whole app down — reads and Swagger still
// work, only the reader sign-in flow is unavailable (LoginEndpoint says so).
var googleClientId = builder.Configuration["GOOGLE_CLIENT_ID"];
var googleClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"];

var authBuilder = builder.Services
    .AddAuthentication(o =>
    {
        // Two audiences in one app, so two cookies. A path policy scheme picks:
        // anything under /api is a blog talking to the API, everything else is
        // the console. Neither handler ever sees the other's cookie.
        o.DefaultScheme = AuthSchemes.ByPath;
        // A policy scheme cannot receive a sign-in, and the Google handler needs
        // somewhere real to write its cookie — so name the reader scheme here.
        o.DefaultSignInScheme = AuthSchemes.Reader;
    })
    .AddPolicyScheme(AuthSchemes.ByPath, "Reader or admin", o =>
        o.ForwardDefaultSelector = ctx =>
            ctx.Request.Path.StartsWithSegments("/api") ? AuthSchemes.Reader : AuthSchemes.Admin)
    .AddCookie(AuthSchemes.Reader, o =>
    {
        o.Cookie.Name = "comments.session";
        // The blog is a different origin, so this cookie is third-party by
        // definition. Secure is not optional once SameSite is None.
        o.Cookie.SameSite = SameSiteMode.None;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        // An API, not a site: an unauthenticated call gets a status code, not a
        // redirect to a login page that does not exist.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    })
    .AddCookie(AuthSchemes.Admin, o =>
    {
        o.Cookie.Name = "komment.admin";
        // Same-origin, so Lax works — and unlike the reader cookie this one still
        // sticks over plain HTTP, which is what `docker compose up` serves.
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/login";
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
    });

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(o =>
    {
        o.ClientId = googleClientId;
        o.ClientSecret = googleClientSecret;
        // Handled by the Google handler itself — this is the URI Google redirects
        // back to and the one registered in the Google Cloud console.
        o.CallbackPath = "/signin-google";
        o.SignInScheme = AuthSchemes.Reader;
        o.ClaimActions.MapJsonKey("picture", "picture");
    });
}

builder.Services.AddAuthorization();

// Both cookies are encrypted with these keys, so a per-process keyring means
// every restart signs everyone out. Only set in Docker — locally the default
// (user profile) store already persists.
if (builder.Configuration["DATAPROTECTION_KEYS"] is { Length: > 0 } keyPath)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath));

var app = builder.Build();
services = app.Services;

// ponytail: migrate on boot so a fresh volume is a working install. Fine for one
// instance; two starting at once would race — move to a one-shot job if this ever
// scales past a single container.
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

// The console answers with HTML error and not-found pages. /api must keep
// answering with status codes — a blog's fetch cannot read an error page — so
// neither wrapper is applied to it.
var isDevelopment = app.Environment.IsDevelopment();

app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"), console =>
{
    if (!isDevelopment) console.UseExceptionHandler("/Error", createScopeForErrors: true);

    console.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
});

if (!isDevelopment) app.UseHsts();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.UseFastEndpoints();

if (isDevelopment)
{
    app.UseSwaggerGen();
}

// ponytail: dropping the admin cookie is enough — signing out of the console does
// not touch a reader's Google session, which is a separate cookie and a separate
// identity even for the same person.
app.MapPost("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(AuthSchemes.Admin);
    return Results.Redirect("/");
});

// Console routes are everything FastEndpoints did not claim.
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
