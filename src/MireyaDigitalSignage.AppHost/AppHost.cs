var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres-server")
    .WithDataVolume("mireya-db")
    .AddDatabase("Postgres");

builder
    .AddProject<Projects.Mireya_Api>("mireya-api")
    .WithReference(postgres)
    .WithEnvironment("provider", "Postgres");

builder.Build().Run();
