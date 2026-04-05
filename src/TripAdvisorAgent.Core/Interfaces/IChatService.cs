using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.Core.Interfaces;

/// <summary>
/// Provides RAG-augmented chat functionality with conversation history.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Sends a chat message and returns the full response.
    /// </summary>
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chat message and streams the response token by token.
    /// </summary>
    IAsyncEnumerable<string> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an existing conversation ID or creates a new one.
    /// </summary>
    string GetOrCreateConversationId(string? conversationId);

    /// <summary>
    /// Deletes a conversation by its ID.
    /// </summary>
    bool DeleteConversation(string conversationId);
}
