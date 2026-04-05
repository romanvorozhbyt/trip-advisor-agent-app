namespace TripAdvisorAgent.Core.Models;

/// <summary>
/// Represents the assistant's response to a chat message.
/// </summary>
public record ChatResponse(string Message, string ConversationId);
