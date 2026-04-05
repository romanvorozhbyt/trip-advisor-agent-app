namespace TripAdvisorAgent.WebApi.Middleware;

public class RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
{   
    public async Task InvokeAsync(HttpContext context)
    {
        await LogRequestAsync(context);

        var originalBody = context.Response.Body;

        // SSE streams must not be buffered — log start/end only
        if (context.Request.Path.StartsWithSegments("/api/chat/stream"))
        {
            logger.LogDebug("[SSE] Streaming response started → {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await next(context);

            logger.LogDebug("[SSE] Streaming response ended ← {StatusCode}",
                context.Response.StatusCode);

            return;
        }

        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await next(context);

        buffer.Position = 0;
        var responseBody = await new StreamReader(buffer).ReadToEndAsync();

        logger.LogDebug(
            """
            [RESPONSE] ← {Method} {Path}
              Status  : {StatusCode}
              Body    : {Body}
            """,
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            Truncate(responseBody));

        buffer.Position = 0;
        await buffer.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }

    private async Task LogRequestAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        var body = string.Empty;
        if (context.Request.ContentLength > 0)
        {
            using var reader = new StreamReader(
                context.Request.Body,
                leaveOpen: true);
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        logger.LogDebug(
            """
            [REQUEST] → {Method} {Path}{Query}
              Headers : Content-Type={ContentType}
              Body    : {Body}
            """,
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            context.Request.ContentType ?? "-",
            Truncate(body));
    }

    private static string Truncate(string value, int maxLength = 2000) =>
        value.Length <= maxLength ? value : value[..maxLength] + " … [truncated]";
}
