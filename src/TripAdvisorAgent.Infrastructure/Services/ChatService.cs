using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// Provides RAG-augmented, agentic chat using Semantic Kernel with automatic function calling.
/// The kernel's registered plugins (e.g. TransportationAgentPlugin) are invoked automatically
/// by the LLM when it needs to fetch real-time data such as flight search results.
/// </summary>
public class ChatService(
    IChatCompletionService chatCompletion,
    IKnowledgeBaseService knowledgeBase,
    Kernel kernel,
    ILogger<ChatService> logger) : IChatService
{
    private readonly ConcurrentDictionary<string, ChatHistory> _conversations = new();

    private static readonly OpenAIPromptExecutionSettings ExecutionSettings = new()
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
    };

    private static string BuildSystemPrompt() => $"""
        You are an expert trip advisor assistant. You help users plan trips, recommend destinations,
        provide travel tips, suggest itineraries, and answer travel-related questions.

        Today's date is {DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}. When the user mentions
        relative dates like "next week", "tomorrow", or "this weekend", calculate the exact date
        relative to today and pass it to the flight search tool in YYYY-MM-DD format.

        You have access to a real-time flight search tool. When the user asks about flights,
        transportation options, or travel routes, use the search_flights function to retrieve
        live data. Always use IATA airport codes (e.g., JFK, LHR, CDG) when calling the tool.
        If the user provides city names, convert them to IATA codes before searching.

        Use the provided knowledge base context to give accurate, helpful advice. If neither the
        context nor the search results contain relevant information, use your general knowledge
        but mention that.

        Be friendly, concise, and practical in your recommendations.
        """;

    /// <inheritdoc />
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();
        var history = _conversations.GetOrAdd(conversationId, _ => new ChatHistory(BuildSystemPrompt()));

        var augmentedMessage = await BuildAugmentedMessageAsync(request.Message, cancellationToken);
        history.AddUserMessage(augmentedMessage);

        logger.LogInformation("[Chat] Sending message to LLM. ConversationId={ConversationId} HistoryLength={Length}",
            conversationId, history.Count);

        var response = await chatCompletion.GetChatMessageContentAsync(
            history, ExecutionSettings, kernel, cancellationToken);
        var assistantMessage = response.Content ?? string.Empty;

        logger.LogInformation("[Chat] LLM responded. ConversationId={ConversationId} FunctionCalls={Calls}",
            conversationId, response.Metadata?.ContainsKey("ToolCalls") == true ? "yes" : "no");

        history.AddAssistantMessage(assistantMessage);

        return new ChatResponse(assistantMessage, conversationId);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> ChatStreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();
        var history = _conversations.GetOrAdd(conversationId, _ => new ChatHistory(BuildSystemPrompt()));

        var augmentedMessage = await BuildAugmentedMessageAsync(request.Message, cancellationToken);
        history.AddUserMessage(augmentedMessage);

        logger.LogInformation("[ChatStream] Sending streaming message to LLM. ConversationId={ConversationId} HistoryLength={Length}",
            conversationId, history.Count);

        var fullResponse = new StringBuilder();
        await foreach (var chunk in chatCompletion.GetStreamingChatMessageContentsAsync(
            history, ExecutionSettings, kernel, cancellationToken))
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
