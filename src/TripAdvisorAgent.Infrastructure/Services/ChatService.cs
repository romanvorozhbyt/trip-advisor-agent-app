using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.SemanticKernel.ChatCompletion;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// Provides RAG-augmented chat using Semantic Kernel chat completion and a knowledge base.
/// </summary>
public class ChatService(
    IChatCompletionService chatCompletion,
    IKnowledgeBaseService knowledgeBase) : IChatService
{
    private readonly ConcurrentDictionary<string, ChatHistory> _conversations = new();

    private const string SystemPrompt = """
        You are an expert trip advisor assistant. You help users plan trips, recommend destinations,
        provide travel tips, suggest itineraries, and answer travel-related questions.

        Use the provided context to give accurate, helpful advice. If the context doesn't contain
        relevant information, use your general knowledge but mention that the information is from
        your general knowledge rather than from the knowledge base.

        Be friendly, concise, and practical in your recommendations.
        """;

    /// <inheritdoc />
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();
        var history = _conversations.GetOrAdd(conversationId, _ => new ChatHistory(SystemPrompt));

        var augmentedMessage = await BuildAugmentedMessageAsync(request.Message, cancellationToken);
        history.AddUserMessage(augmentedMessage);

        var response = await chatCompletion.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
        var assistantMessage = response.Content ?? string.Empty;

        history.AddAssistantMessage(assistantMessage);

        return new ChatResponse(assistantMessage, conversationId);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatStreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();
        var history = _conversations.GetOrAdd(conversationId, _ => new ChatHistory(SystemPrompt));

        var augmentedMessage = await BuildAugmentedMessageAsync(request.Message, cancellationToken);
        history.AddUserMessage(augmentedMessage);

        var fullResponse = new StringBuilder();
        await foreach (var chunk in chatCompletion.GetStreamingChatMessageContentsAsync(history, cancellationToken: cancellationToken))
        {
            if (chunk.Content is not null)
            {
                fullResponse.Append(chunk.Content);
                yield return chunk.Content;
            }
        }

        history.AddAssistantMessage(fullResponse.ToString());
    }

    /// <inheritdoc />
    public string GetOrCreateConversationId(string? conversationId)
    {
        return conversationId ?? Guid.NewGuid().ToString();
    }

    /// <inheritdoc />
    public bool DeleteConversation(string conversationId)
    {
        return _conversations.TryRemove(conversationId, out _);
    }

    private async Task<string> BuildAugmentedMessageAsync(string userMessage, CancellationToken cancellationToken)
    {
        var relevantDocs = await knowledgeBase.SearchAsync(userMessage, cancellationToken: cancellationToken);

        if (relevantDocs.Count == 0)
            return userMessage;

        var context = string.Join("\n---\n", relevantDocs);
        return $"""
            [Relevant context from knowledge base]:
            {context}

            [User question]:
            {userMessage}
            """;
    }
}
