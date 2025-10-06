using Microsoft.EntityFrameworkCore;
using Mireya.Api.Startup;
using Mireya.Database;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration
    .AddJsonFile("appsettings.json", false, true)
    .AddJsonFile("appsettings.Development.json", true, true)
    .AddUserSecrets<Program>(true, true)
    .AddEnvironmentVariables().Build();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddMireyaDbContext(config);

var app = builder.Build();

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
var context = services.GetRequiredService<MireyaDbContext>();

// Apply pending migrations automatically
await context.Database.MigrateAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    var db = services.GetRequiredService<MireyaDbContext>();
    await MireyaDbContext.InitializeAsync(db);

    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
