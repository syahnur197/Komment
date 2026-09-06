var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddPostgres("postgres")
    // Without this the data is gone every time the AppHost stops.
    .WithDataVolume()
    .AddDatabase("comments");

// ponytail: plain executable, not AddViteApp — `npm run dev` is a watch build,
// not a dev server, so there is no endpoint for Aspire to model or health-check.
// Blazor serves wwwroot/dist with no-cache and a stable fingerprint, so a
// browser refresh picks up a rebuild without restarting the app.
builder.AddExecutable("tailwind", "npm", "../Backend", "run", "dev")
    .ExcludeFromManifest();

// One app: the API blogs call and the console admins use are the same process,
// so there is nothing left for Aspire to wire between them.
builder.AddProject<Projects.Backend>("komment")
    // Injects ConnectionStrings__comments; Backend reads it with plain
    // GetConnectionString, no Aspire client integration.
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
