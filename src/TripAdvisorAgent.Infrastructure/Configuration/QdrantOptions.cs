namespace TripAdvisorAgent.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the Qdrant vector database connection.
/// </summary>
public class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 6334;

    public bool UseHttps { get; set; } = false;

    public string? ApiKey { get; set; }
}
