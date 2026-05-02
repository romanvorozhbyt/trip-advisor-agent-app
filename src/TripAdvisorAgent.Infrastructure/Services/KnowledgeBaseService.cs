using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Infrastructure.Models;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// Manages the vector-store-backed knowledge base using Semantic Kernel and an embedding generator.
/// </summary>
public class KnowledgeBaseService(
    VectorStore vectorStore,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) : IKnowledgeBaseService
{
    private const string CollectionName = "trip-knowledge";
    private readonly VectorStoreCollection<Guid, TripKnowledgeRecord> _collection =
        vectorStore.GetCollection<Guid, TripKnowledgeRecord>(CollectionName);

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _collection.EnsureCollectionExistsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task IngestAsync(string content, string category, Guid? id = null, CancellationToken cancellationToken = default)
    {
        var embedding = await embeddingGenerator.GenerateVectorAsync(content, cancellationToken: cancellationToken);

        var record = new TripKnowledgeRecord
        {
            Id = id ?? Guid.NewGuid(),
            Content = content,
            Category = category,
            ContentEmbedding = embedding
        };

        await _collection.UpsertAsync(record, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RecordExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _collection.GetAsync(id, cancellationToken: cancellationToken);
        return record is not null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> SearchAsync(string query, int topK, CancellationToken cancellationToken)
    {
        var queryEmbedding = await embeddingGenerator.GenerateVectorAsync(query, cancellationToken: cancellationToken);

        var results = new List<string>();
        await foreach (var result in _collection.SearchAsync(queryEmbedding, topK, cancellationToken: cancellationToken))
        {
            results.Add(result.Record.Content);
        }

        return results;
    }
}
