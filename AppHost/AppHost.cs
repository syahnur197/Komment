var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("postgres")
    // Without this the data is gone every time the AppHost stops.
    .WithDataVolume()
    .AddDatabase("comments");

var backend = builder.AddProject<Projects.Backend>("backend")
    // Injects ConnectionStrings__comments; Backend reads it with plain
    // GetConnectionString, no Aspire client integration.
    .WithReference(db)
    .WaitFor(db)
    // The console calls this API from the browser, so it needs a CORS entry.
    // Literal rather than an endpoint reference because Dashboard already
    // references backend, and pointing them at each other is a cycle Aspire
    // has to resolve at graph-build time. The port is pinned in the Dashboard's
    // launchSettings.json, so it is not really dynamic.
    .WithEnvironment("DASHBOARD_ORIGIN", "https://localhost:7222,http://localhost:5191");

// ponytail: plain executable, not AddViteApp — `npm run dev` is a watch build,
// not a dev server, so there is no endpoint for Aspire to model or health-check.
// Blazor serves wwwroot/dist with no-cache and a stable fingerprint, so a
// browser refresh picks up a rebuild without restarting the app.
builder.AddExecutable("tailwind", "npm", "../Dashboard", "run", "dev")
    .ExcludeFromManifest();

builder.AddProject<Projects.Dashboard>("dashboard")
    // The reference sets services__backend__https__0 / __http__0. The Dashboard
    // server does not call the API any more — it reads those to learn the URL to
    // hand the browser, which works here only because in development the address
    // the server sees and the address the browser sees are the same. Docker sets
    // BACKEND_PUBLIC_URL instead, where they are not.
    .WithReference(backend)
    .WaitFor(backend);

builder.Build().Run();
