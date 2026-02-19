namespace Qdrant.Demo.Api.Endpoints;

public static class InfoEndpoints
{
    public static WebApplication MapInfoEndpoints(this WebApplication app, object info)
    {
        app.MapGet("/api/info", () => Results.Ok(info));

        app.MapGet("/health", () => Results.Ok("healthy"))
            .ExcludeFromDescription();

        return app;
    }
}
