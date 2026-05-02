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
using TripAdvisorAgent.Infrastructure.Services.Amadeus;
using TripAdvisorAgent.Infrastructure.Services.AirLabs;

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

        // Flight provider options — no ValidateOnStart so the app starts when only one provider is configured
        services.AddOptions<AirLabsOptions>()
            .Bind(configuration.GetSection(AirLabsOptions.SectionName));

        services.AddOptions<AmadeusOptions>()
            .Bind(configuration.GetSection(AmadeusOptions.SectionName));

        services.AddOptions<NewsOptions>()
            .Bind(configuration.GetSection(NewsOptions.SectionName));

        // Semantic Kernel + OpenAI services
        var section = configuration.GetSection(GitHubModelsOptions.SectionName);
        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(section["ApiKey"] ?? throw new InvalidOperationException("GitHubModels:ApiKey is required.")),
            new OpenAIClientOptions { Endpoint = new Uri(section["Endpoint"] ?? throw new InvalidOperationException("GitHubModels:Endpoint is required.")) });

        var kernelBuilder = services.AddKernel();
        kernelBuilder.AddOpenAIChatCompletion(section["ModelId"] ?? throw new InvalidOperationException("GitHubModels:ModelId is required."), openAiClient);
        kernelBuilder.AddOpenAIEmbeddingGenerator(section["EmbeddingModelId"] ?? throw new InvalidOperationException("GitHubModels:EmbeddingModelId is required."), openAiClient);
        kernelBuilder.Plugins.AddFromType<TransportationAgentPlugin>("Transportation");

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

        // HTTP clients for external APIs
        services.AddHttpClient("amadeus");
        services.AddHttpClient("airlabs");
        services.AddHttpClient("news", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TripAdvisorAgent/1.0 (+news-ingestion)");
        });

        // Flight search providers — both registered as keyed singletons; the factory
        // resolves the active one lazily, so only the configured provider's options are
        // ever accessed (and therefore validated).
        services.AddKeyedSingleton<IFlightSearchProvider, AmadeusFlightProvider>("Amadeus");
        services.AddKeyedSingleton<IFlightSearchProvider, AirLabsFlightProvider>("AirLabs");
        services.AddSingleton<IFlightSearchProviderFactory, FlightSearchProviderFactory>();
        services.AddSingleton<ITransportationService, TransportationService>();

        // Application Services
        services.AddSingleton<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddSingleton<IChatService, ChatService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();

        // News ingestion runner + background scheduler
        services.AddSingleton<NewsIngestionRunner>();
        services.AddHostedService<NewsIngestionService>();

        return services;
    }

    /// <summary>
    /// Registers only the services required for the news-ingestion Lambda function:
    /// Semantic Kernel embedding generator, Qdrant vector store, <see cref="IKnowledgeBaseService"/>,
    /// the "news" HTTP client, and <see cref="NewsIngestionRunner"/>.
    /// Does not register JWT auth, SQLite, flight providers, or hosted background services.
    /// </summary>
    public static IServiceCollection AddNewsIngestionServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GitHubModelsOptions>()
            .Bind(configuration.GetSection(GitHubModelsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<QdrantOptions>()
            .Bind(configuration.GetSection(QdrantOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<NewsOptions>()
            .Bind(configuration.GetSection(NewsOptions.SectionName));

        // When deployed as a Lambda the array cannot be expressed as a single env var.
        // If RssFeeds is empty but RssFeedsRaw is set, split the comma-delimited string.
        services.PostConfigure<NewsOptions>(o =>
        {
            if (o.RssFeeds.Length == 0 && !string.IsNullOrWhiteSpace(o.RssFeedsRaw))
                o.RssFeeds = o.RssFeedsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        });

        var ghSection = configuration.GetSection(GitHubModelsOptions.SectionName);
        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(ghSection["ApiKey"] ?? throw new InvalidOperationException("GitHubModels:ApiKey is required.")),
            new OpenAIClientOptions { Endpoint = new Uri(ghSection["Endpoint"] ?? throw new InvalidOperationException("GitHubModels:Endpoint is required.")) });

        var kernelBuilder = services.AddKernel();
        kernelBuilder.AddOpenAIEmbeddingGenerator(
            ghSection["EmbeddingModelId"] ?? throw new InvalidOperationException("GitHubModels:EmbeddingModelId is required."),
            openAiClient);

        var qdrant = configuration.GetSection(QdrantOptions.SectionName);
        services.AddQdrantVectorStore(
            qdrant["Host"] ?? throw new InvalidOperationException("Qdrant:Host is required."),
            int.Parse(qdrant["Port"] ?? throw new InvalidOperationException("Qdrant:Port is required.")),
            bool.Parse(qdrant["UseHttps"] ?? throw new InvalidOperationException("Qdrant:UseHttps is required.")),
            qdrant["ApiKey"]);

        services.AddHttpClient("news", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TripAdvisorAgent/1.0 (+news-ingestion)");
        });

        services.AddSingleton<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddSingleton<NewsIngestionRunner>();

        return services;
    }
}
