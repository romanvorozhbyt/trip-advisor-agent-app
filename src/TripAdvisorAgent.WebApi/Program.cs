using Microsoft.EntityFrameworkCore;
using Serilog;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Infrastructure;
using TripAdvisorAgent.Infrastructure.Data;
using TripAdvisorAgent.Infrastructure.Services;
using TripAdvisorAgent.WebApi.Endpoints;
using TripAdvisorAgent.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/app-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
    ));

// Infrastructure (Semantic Kernel, Vector Store, SQLite, Services)
builder.Services.AddInfrastructure(builder.Configuration);

// CORS (chat client is a separate application)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// Apply pending SQLite migrations / ensure DB is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Seed knowledge base
await KnowledgeBaseSeeder.SeedAsync(app.Services.GetRequiredService<IKnowledgeBaseService>());

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.UseMiddleware<RequestResponseLoggingMiddleware>();

app.MapAuthEndpoints();
app.MapChatEndpoints();
app.MapKnowledgeEndpoints();
app.MapNewsIngestionEndpoints();
app.MapUserEndpoints();

app.Run();
