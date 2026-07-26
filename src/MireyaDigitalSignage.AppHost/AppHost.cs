var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres-server")
    // Keep major-version upgrades explicit: PostgreSQL data directories cannot
    // be upgraded safely by changing the container image alone.
    .WithImageTag("18")
    .WithDataVolume("mireya-db-18")
    .AddDatabase("Postgres");

builder
    .AddProject<Projects.Mireya_Api>("mireya-api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithEnvironment("provider", "Postgres");

await builder.Build().RunAsync();
