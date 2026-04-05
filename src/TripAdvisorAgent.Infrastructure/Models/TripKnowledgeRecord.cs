using Microsoft.Extensions.VectorData;

namespace TripAdvisorAgent.Infrastructure.Models;

/// <summary>
/// Vector store record representing a piece of trip-related knowledge with its embedding.
/// </summary>
public class TripKnowledgeRecord
{
    [VectorStoreKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [VectorStoreData(IsIndexed = true)]
    public string Category { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Content { get; set; } = string.Empty;

    [VectorStoreVector(1536)]
    public ReadOnlyMemory<float> ContentEmbedding { get; set; }
}
