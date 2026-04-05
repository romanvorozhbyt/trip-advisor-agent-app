namespace TripAdvisorAgent.Core.Models;

/// <summary>
/// Represents a request to ingest new knowledge into the RAG knowledge base.
/// </summary>
public record IngestKnowledgeRequest(string Content, string Category = "general");
