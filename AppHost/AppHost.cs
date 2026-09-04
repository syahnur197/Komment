var builder = DistributedApplication.CreateBuilder(args);

var backend = builder.AddProject<Projects.Backend>("backend");

builder.AddProject<Projects.Dashboard>("dashboard")
    // ponytail: reference only sets ConnectionStrings/services__backend__* env vars.
    // Dashboard needs Microsoft.Extensions.ServiceDiscovery to resolve
    // "https+http://backend" — add it when the frontend actually calls the API.
    .WithReference(backend)
    .WaitFor(backend);

builder.Build().Run();
