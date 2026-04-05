using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.WebApi.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/chat").RequireAuthorization();

        group.MapPost("/", async (ChatRequest request, IChatService chatService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return Results.BadRequest(new { error = "Message is required." });

            var response = await chatService.ChatAsync(request, ct);
            return Results.Ok(response);
        });

        group.MapPost("/stream", async (ChatRequest request, IChatService chatService, HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { error = "Message is required." });
                return;
            }

            var conversationId = chatService.GetOrCreateConversationId(request.ConversationId);
            var streamRequest = request with { ConversationId = conversationId };

            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";

            await context.Response.WriteAsync($"event: metadata\ndata: {{\"conversationId\":\"{conversationId}\"}}\n\n");
            await context.Response.Body.FlushAsync();

            await foreach (var chunk in chatService.ChatStreamAsync(streamRequest, context.RequestAborted))
            {
                await context.Response.WriteAsync($"data: {chunk}\n\n");
                await context.Response.Body.FlushAsync();
            }

            await context.Response.WriteAsync("data: [DONE]\n\n");
            await context.Response.Body.FlushAsync();
        });

        group.MapDelete("/{conversationId}", (string conversationId, IChatService chatService) =>
        {
            return chatService.DeleteConversation(conversationId)
                ? Results.NoContent()
                : Results.NotFound();
        });
    }
}
