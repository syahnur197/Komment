using Backend.Data;
using Backend.Features.Sites;
using FastEndpoints;
using FastEndpoints.Swagger;
using Backend.Features.Auth;
using Backend.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// Named once: the scheme that decides between bearer and cookie per request.
const string SmartScheme = "smart";

var builder = WebApplication.CreateBuilder(args);

// Scans the assembly at startup for every Endpoint/Validator class and registers
// them. There is no route table to maintain — the endpoint classes are the routes.
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("comments")));

// The rules live here, not in the endpoints. Endpoints bind a request and turn a
// Result into a status code; what is allowed is decided once, in a service.
builder.Services.AddScoped<SiteService>();
builder.Services.AddScoped<CommentService>();
builder.Services.AddScoped<AccountService>();

// Every browser client is on another origin: blogs are static sites, and the
// Dashboard is a browser app talking to this API directly. Blogs come from the
// Sites table (credentials on, because readers use the cross-site cookie); the
// Dashboard comes from DASHBOARD_ORIGIN and carries a bearer token instead.
// Assigned after Build() below because the policy is declared before the
// provider exists.
IServiceProvider? services = null;

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .SetIsOriginAllowed(origin => SiteOrigins.IsAllowed(services!, origin))
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// Missing credentials must not take the whole app down — reads and Swagger still
// work, only the sign-in flow is unavailable (LoginEndpoint says so).
var googleClientId = builder.Configuration["GOOGLE_CLIENT_ID"];
var googleClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"];

// Minted per process in Development, required everywhere else. Registered so
// the token endpoint can take it by property injection like any dependency.
var signingKey = Tokens.SigningKey(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(signingKey);

var authBuilder = builder.Services
    .AddAuthentication(o =>
    {
        // Two credentials reach this API: the blog's cross-site session cookie
        // and the Dashboard's bearer token. A policy scheme picks per request so
        // no endpoint has to know which one it got.
        o.DefaultScheme = SmartScheme;
        // A policy scheme cannot receive a sign-in, and the Google handler needs
        // somewhere real to write its cookie — so name the cookie scheme here.
        o.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddPolicyScheme(SmartScheme, "Bearer or cookie", o =>
        o.ForwardDefaultSelector = ctx =>
            ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? JwtBearerDefaults.AuthenticationScheme
                : CookieAuthenticationDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Claims were written under the exact names UserClaims uses. The default
        // inbound mapping rewrites well-known ones on the way back in, which
        // would leave Roles() and UserIdOf looking for claims that moved.
        o.MapInboundClaims = false;
        o.TokenValidationParameters = Tokens.Validation(signingKey);
    })
    .AddCookie(o =>
    {
        o.Cookie.Name = "comments.session";
        o.Cookie.SameSite = SameSiteMode.None;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
        // An API, not a site: an unauthenticated call gets a status code, not a
        // redirect to a login page that does not exist.
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
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
        o.ClaimActions.MapJsonKey("picture", "picture");
    });
}

builder.Services.AddAuthorization();

// The session cookie is encrypted with these keys, so a per-process keyring means
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

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerGen();
}

app.Run();
