using Dashboard;
using Dashboard.Components;

var builder = WebApplication.CreateBuilder(args);

// Static SSR only. Nothing here is interactive and nothing here calls the API:
// this app renders HTML shells, and the browser talks to the Backend directly.
builder.Services.AddRazorComponents();

// The only API knowledge left in this project — the public URL to hand the
// browser. No HttpClient, no session handling, no auth: the Dashboard server
// does not know or need to know who is signed in.
builder.Services.AddSingleton<ApiBaseUrl>();

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

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();
