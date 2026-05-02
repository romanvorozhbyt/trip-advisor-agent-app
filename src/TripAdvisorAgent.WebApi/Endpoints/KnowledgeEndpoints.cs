using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.WebApi.Endpoints;

public static class KnowledgeEndpoints
{
    public static void MapKnowledgeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/knowledge").RequireAuthorization();

        group.MapPost("/", async (IngestKnowledgeRequest request, IKnowledgeBaseService knowledgeBase, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return Results.BadRequest(new { error = "Content is required." });

            await knowledgeBase.IngestAsync(request.Content, request.Category, cancellationToken: ct);
            return Results.Created();
        });
    }
}
