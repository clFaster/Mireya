var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres-server")
    .WithDataVolume("mireya-db")
    .AddDatabase("Postgres");

var mireya = builder
    .AddProject<Projects.Mireya_Api>("mireya-api")
    .WithReference(postgres)
    .WaitFor(postgres)
    .WithEnvironment("provider", "Postgres");

await builder.Build().RunAsync();
