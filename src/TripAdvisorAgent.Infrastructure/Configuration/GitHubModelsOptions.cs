using System.ComponentModel.DataAnnotations;

namespace TripAdvisorAgent.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the GitHub Models / OpenAI-compatible endpoint.
/// </summary>
public class GitHubModelsOptions
{
    public const string SectionName = "GitHubModels";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    public string Endpoint { get; set; } = "https://models.inference.ai.azure.com";

    public string ModelId { get; set; } = "gpt-4o-mini";

    public string EmbeddingModelId { get; set; } = "text-embedding-3-small";
}
