using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using OpenAI.Embeddings;
using FailedTests.Api.Models;
using static Qdrant.Client.Grpc.Conditions;
using static FailedTests.Api.Helpers.QdrantPayloadHelpers;

namespace FailedTests.Api.Endpoints;

public static class SearchEndpoints
{
    public static WebApplication MapSearchEndpoints(this WebApplication app, string collectionName)
    {
        // -------------------------------------------------
        // Vector similarity search + optional metadata filters
        // -------------------------------------------------
        app.MapPost("/search/similar", async (
            [FromBody] SimilaritySearchRequest req,
            QdrantClient qdrant,
            EmbeddingClient embeddings) =>
        {
            try
            {
                var embedding = await embeddings.GenerateEmbeddingAsync(req.QueryText);
                var vector = embedding.Value.ToFloats().ToArray();

                // Build an optional metadata filter
                Condition? filter = null;

                if (!string.IsNullOrWhiteSpace(req.ProjectName))
                    filter = MatchKeyword("project_name", req.ProjectName);

                if (!string.IsNullOrWhiteSpace(req.DefinitionName))
                    filter = filter is null
                        ? MatchKeyword("definition_name", req.DefinitionName)
                        : filter & MatchKeyword("definition_name", req.DefinitionName);

                if (req.FromTimestampMs is not null || req.ToTimestampMs is not null)
                {
                    // GOTCHA: Must fully qualify Qdrant.Client.Grpc.Range to avoid System.Range ambiguity.
                    // Gte/Lte are double (not double?), so assign conditionally.
                    var range = new Qdrant.Client.Grpc.Range();
                    if (req.FromTimestampMs is not null) range.Gte = (double)req.FromTimestampMs.Value;
                    if (req.ToTimestampMs is not null)   range.Lte = (double)req.ToTimestampMs.Value;
                    var timeCond = Range("timestamp_ms", range);

                    filter = filter is null ? timeCond : filter & timeCond;
                }

                // GOTCHA: SearchAsync expects Filter? (not Condition?). Wrap in Filter.
                var searchFilter = filter is null ? null : new Filter { Must = { filter } };

                var hits = await qdrant.SearchAsync(
                    collectionName: collectionName,
                    vector: vector,
                    limit: (ulong)req.Limit,       // safety cap, not the primary control
                    filter: searchFilter,
                    scoreThreshold: req.ScoreThreshold,
                    payloadSelector: true);         // GOTCHA: not "withPayload"

                var response = hits.Select(h => new
                {
                    id = h.Id?.Uuid ?? h.Id?.Num.ToString(),
                    score = h.Score,
                    payload = PayloadToDictionary(h.Payload)
                });

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[search/similar] Error: {ex.Message}");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Similarity search failed");
            }
        });

        // -------------------------------------------------
        // Metadata-only search (no vector) via Qdrant REST scroll
        // -------------------------------------------------
        app.MapPost("/search/metadata", async (
            [FromBody] MetadataSearchRequest req,
            IHttpClientFactory httpFactory) =>
        {
            try
            {
                var http = httpFactory.CreateClient("qdrant-http");

                object? filter = BuildScrollFilter(req);

                var body = new
                {
                    limit = req.Limit,
                    with_payload = true,
                    with_vector = false,
                    filter
                };

                var resp = await http.PostAsJsonAsync($"collections/{collectionName}/points/scroll", body);
                resp.EnsureSuccessStatusCode();

                var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
                return Results.Json(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[search/metadata] Error: {ex.Message}");
                return Results.Problem(detail: ex.Message, statusCode: 500, title: "Metadata search failed");
            }
        });

        return app;
    }
}
