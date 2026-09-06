using Backend.Components;
using Backend.Data;
using Backend.Features.Auth;
using Backend.Features.Sites;
using Backend.Services;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

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
    if (trimmed.Split('=', 2) is not [var name, var value]) continue;

    // A real environment variable wins — .env is the local-dev fallback, not an
    // override of whatever the host set.
    if (Environment.GetEnvironmentVariable(name.Trim()) is null)
        Environment.SetEnvironmentVariable(name.Trim(), value.Trim());
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
// allowed origins are the registered sites, read per preflight — which needs a
// service provider, so the policy is configured through the options pipeline
// rather than inline: that hands us one instead of capturing it after Build().
// The console needs no CORS entry at all: it is served from this origin.
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>().Configure<IServiceProvider>((corsOptions, services) =>
    corsOptions.AddDefaultPolicy(policy => policy
        .SetIsOriginAllowed(origin => SiteOrigins.IsAllowed(services, origin))
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// Missing credentials must not take the whole app down — reads and Swagger still
// work, only the reader sign-in flow is unavailable (LoginEndpoint says so).
var googleClientId = builder.Configuration["GOOGLE_CLIENT_ID"];
var googleClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"];

var authBuilder = builder.Services
    .AddAuthentication(options =>
    {
        // Two audiences in one app, so two cookies. A path policy scheme picks:
        // anything under /api is a blog talking to the API, everything else is
        // the console. Neither handler ever sees the other's cookie.
        options.DefaultScheme = AuthSchemes.ByPath;
        // A policy scheme cannot receive a sign-in, and the Google handler needs
        // somewhere real to write its cookie — so name the reader scheme here.
        options.DefaultSignInScheme = AuthSchemes.Reader;
    })
    .AddPolicyScheme(AuthSchemes.ByPath, "Reader or admin", options =>
        options.ForwardDefaultSelector = httpContext =>
            httpContext.Request.Path.StartsWithSegments("/api") ? AuthSchemes.Reader : AuthSchemes.Admin)
    .AddCookie(AuthSchemes.Reader, options =>
    {
        options.Cookie.Name = "comments.session";
        // The blog is a different origin, so this cookie is third-party by
        // definition. Secure is not optional once SameSite is None.
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        // An API, not a site: an unauthenticated call gets a status code, not a
        // redirect to a login page that does not exist.
        options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
    })
    .AddCookie(AuthSchemes.Admin, options =>
    {
        options.Cookie.Name = "komment.admin";
        // Same-origin, so Lax works — and unlike the reader cookie this one still
        // sticks over plain HTTP, which is what `docker compose up` serves.
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        // Handled by the Google handler itself — this is the URI Google redirects
        // back to and the one registered in the Google Cloud console.
        options.CallbackPath = "/signin-google";
        options.SignInScheme = AuthSchemes.Reader;
        options.ClaimActions.MapJsonKey("picture", "picture");
    });
}

builder.Services.AddAuthorization();

// Fixed window on the API only — the console is a Blazor circuit, not a request
// per interaction, and throttling it would break the UI. Keyed by user when
// signed in so one office NAT is not one bucket, which is why UseRateLimiter
// below sits after UseAuthentication.
// ponytail: the built-in limiter counts in this process, so the window resets on
// deploy and a second replica doubles the real limit. One container, so that is
// the right trade — move the counter to Redis if this ever scales out.
var rateLimitPerMinute = builder.Configuration.GetValue("RATE_LIMIT_PER_MINUTE", 30);

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (!httpContext.Request.Path.StartsWithSegments("/api"))
            return RateLimitPartition.GetNoLimiter("console");

        var caller = UserClaims.UserIdOf(httpContext.User)?.ToString()
                     ?? httpContext.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(caller, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitPerMinute,
            Window = TimeSpan.FromMinutes(1),
        });
    });

    // A blog's fetch gets a status code and a hint, never an error page.
    options.OnRejected = (rejectedContext, _) =>
    {
        rejectedContext.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (rejectedContext.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            rejectedContext.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

        return ValueTask.CompletedTask;
    };
});

// Both cookies are encrypted with these keys, so a per-process keyring means
// every restart signs everyone out. Only set in Docker — locally the default
// (user profile) store already persists.
if (builder.Configuration["DATAPROTECTION_KEYS"] is { Length: > 0 } keyPath)
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath));

var app = builder.Build();

// ponytail: migrate on boot so a fresh volume is a working install. Fine for one
// instance; two starting at once would race — move to a one-shot job if this ever
// scales past a single container.
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

// The console answers with HTML error and not-found pages. /api must keep
// answering with status codes — a blog's fetch cannot read an error page — so
// neither wrapper is applied to it.
var isDevelopment = app.Environment.IsDevelopment();

app.UseWhen(httpContext => !httpContext.Request.Path.StartsWithSegments("/api"), console =>
{
    if (!isDevelopment) console.UseExceptionHandler("/Error", createScopeForErrors: true);

    console.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
});

if (!isDevelopment) app.UseHsts();

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.UseFastEndpoints();

if (isDevelopment) app.UseSwaggerGen();

app.MapPost("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(AuthSchemes.Admin);
    return Results.Redirect("/");
});

// Console routes are everything FastEndpoints did not claim.
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
