using Microsoft.AspNetCore.Mvc;
using FailedTests.Api.Models;
using FailedTests.Api.Services;

namespace FailedTests.Api.Endpoints;

public static class IndexEndpoints
{
    public static WebApplication MapIndexEndpoints(this WebApplication app, string collectionName)
    {
        app.MapPost("/index/test-result", async (
            [FromBody] FailedTestEnvelope env,
            ITestResultIndexer indexer,
            CancellationToken ct) =>
        {
            try
            {
                var response = await indexer.IndexTestResultAsync(env, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[index/test-result] Error: {ex.Message}");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Indexing failed");
            }
        });

        return app;
    }
}
