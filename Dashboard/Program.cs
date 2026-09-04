using Dashboard;
using Dashboard.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/login";
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// The Dashboard is a *client* of the API: it holds the backend's session cookie
// server-side (as a claim on its own cookie) and replays it on every call.
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BackendSessionHandler>();
builder.Services.AddServiceDiscovery();
builder.Services.AddHttpClient(BackendSessionHandler.ClientName,
        c => c.BaseAddress = new Uri("https+http://backend"))
    // UseCookies defaults to true, and IHttpClientFactory pools one primary
    // handler for every caller of this client — so its CookieContainer would
    // collect each user's API session and replay the most recent one for
    // everybody. The session must come only from BackendSessionHandler, which
    // reads it per request off the signed-in user.
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false })
    .AddServiceDiscovery()
    .AddHttpMessageHandler<BackendSessionHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// ponytail: dropping our cookie is enough — the backend session it carried is
// unreachable once the claim is gone. Call the API's /api/auth/logout too if
// server-side revocation ever matters.
app.MapPost("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
