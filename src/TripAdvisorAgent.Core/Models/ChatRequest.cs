namespace TripAdvisorAgent.Core.Models;

/// <summary>
/// Represents an incoming chat message from the user.
/// </summary>
public record ChatRequest(string Message, string? ConversationId = null);
