using System.ClientModel;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using OpenAI;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Infrastructure.Configuration;
using TripAdvisorAgent.Infrastructure.Data;
using TripAdvisorAgent.Infrastructure.Services;

namespace TripAdvisorAgent.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure services into the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Semantic Kernel, vector store, and application services.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Strongly-typed options with validation
        services.AddOptions<GitHubModelsOptions>()
            .Bind(configuration.GetSection(GitHubModelsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection(QdrantOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<GoogleAuthOptions>()
            .Bind(configuration.GetSection(GoogleAuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Semantic Kernel + OpenAI services
        var section = configuration.GetSection(GitHubModelsOptions.SectionName);
        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(section["ApiKey"] ?? throw new InvalidOperationException("GitHubModels:ApiKey is required.")),
            new OpenAIClientOptions { Endpoint = new Uri(section["Endpoint"] ?? throw new InvalidOperationException("GitHubModels:Endpoint is required.")) });

        var kernelBuilder = services.AddKernel();
        kernelBuilder.AddOpenAIChatCompletion(section["ModelId"] ?? throw new InvalidOperationException("GitHubModels:ModelId is required."), openAiClient);
        kernelBuilder.AddOpenAIEmbeddingGenerator(section["EmbeddingModelId"] ?? throw new InvalidOperationException("GitHubModels:EmbeddingModelId is required."), openAiClient);

        // Vector Store — Qdrant
        var qdrant = configuration.GetSection(QdrantOptions.SectionName);
        services.AddQdrantVectorStore(
            qdrant["Host"] ?? throw new InvalidOperationException("Qdrant:Host is required."),
            int.Parse(qdrant["Port"] ?? throw new InvalidOperationException("Qdrant:Port is required.")),
            bool.Parse(qdrant["UseHttps"] ?? throw new InvalidOperationException("Qdrant:UseHttps is required.")),
            qdrant["ApiKey"]);

        // SQLite — user data
        var connectionString = configuration.GetConnectionString("Sqlite") ?? throw new InvalidOperationException("ConnectionStrings:Sqlite is required.");
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        // JWT Bearer Authentication
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSection["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is required."),
                    ValidAudience = jwtSection["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is required."),
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSection["Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required."))),
                };
            });
        services.AddAuthorization();

        // Application Services
        services.AddSingleton<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddSingleton<IChatService, ChatService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
