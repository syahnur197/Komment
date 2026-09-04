var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("postgres")
    // Without this the data is gone every time the AppHost stops.
    .WithDataVolume()
    .AddDatabase("comments");

var backend = builder.AddProject<Projects.Backend>("backend")
    // Injects ConnectionStrings__comments; Backend reads it with plain
    // GetConnectionString, no Aspire client integration.
    .WithReference(db)
    .WaitFor(db);

// ponytail: plain executable, not AddViteApp — `npm run dev` is a watch build,
// not a dev server, so there is no endpoint for Aspire to model or health-check.
// Blazor serves wwwroot/dist with no-cache and a stable fingerprint, so a
// browser refresh picks up a rebuild without restarting the app.
builder.AddExecutable("tailwind", "npm", "../Dashboard", "run", "dev")
    .ExcludeFromManifest();

builder.AddProject<Projects.Dashboard>("dashboard")
    // ponytail: reference only sets ConnectionStrings/services__backend__* env vars.
    // Dashboard needs Microsoft.Extensions.ServiceDiscovery to resolve
    // "https+http://backend" — add it when the frontend actually calls the API.
    .WithReference(backend)
    .WaitFor(backend);

builder.Build().Run();
