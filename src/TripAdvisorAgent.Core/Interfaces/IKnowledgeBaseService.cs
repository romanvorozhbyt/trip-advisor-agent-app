namespace TripAdvisorAgent.Core.Interfaces;

/// <summary>
/// Manages the vector-store-backed knowledge base for RAG retrieval.
/// </summary>
public interface IKnowledgeBaseService
{
    /// <summary>
    /// Ensures the underlying vector collection is created and ready.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests a new piece of knowledge, generating its embedding and storing it.
    /// </summary>
    Task IngestAsync(string content, string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches the knowledge base for entries semantically similar to the query.
    /// </summary>
    Task<IReadOnlyList<string>> SearchAsync(string query, int topK = 3, CancellationToken cancellationToken = default);
}
