using Microsoft.AspNetCore.Mvc;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using OpenAI.Embeddings;
using FailedTests.Api.Models;
using static FailedTests.Api.Helpers.TextHelpers;

namespace FailedTests.Api.Endpoints;

public static class IndexEndpoints
{
    public static WebApplication MapIndexEndpoints(this WebApplication app, string collectionName)
    {
        app.MapPost("/index/test-result", async (
            [FromBody] FailedTestEnvelope env,
            QdrantClient qdrant,
            EmbeddingClient embeddings) =>
        {
            try
            {
                var testName = PickTestName(env.Result);

                // Deterministic point-id: idempotent per build/run/result
                var pointId = DeterministicGuid(
                    $"ado|{env.ProjectName}|{env.BuildId}|{env.TestRunId}|{env.Result.Id}");

                // Deterministic signature-id: groups similar failures across builds
                var signatureId = DeterministicGuid(
                    $"sig|{env.ProjectName}|{env.DefinitionName}|{testName}" +
                    $"|{Normalize(env.Result.ErrorMessage)}|{NormalizeStack(env.Result.StackTrace)}");

                var embeddingText = BuildEmbeddingText(env, testName);
                var embedding = await embeddings.GenerateEmbeddingAsync(embeddingText);
                var vector = embedding.Value.ToFloats().ToArray();

                var timestampMs = ToUnixMs(
                    env.Result.CompletedDate ?? env.Result.StartedDate ?? DateTime.UtcNow);

                var point = new PointStruct
                {
                    Id = new PointId { Uuid = pointId.ToString("D") },
                    Vectors = vector,
                    Payload =
                    {
                        ["project_name"]        = env.ProjectName,
                        ["definition_name"]     = env.DefinitionName,
                        ["build_id"]            = env.BuildId,
                        ["build_name"]          = env.BuildName,
                        ["test_run_id"]         = env.TestRunId,
                        ["test_result_id"]      = env.Result.Id,
                        ["test_name"]           = testName,
                        ["automated_test_name"] = env.Result.AutomatedTestName ?? "",
                        ["computer_name"]       = env.Result.ComputerName ?? "",
                        ["outcome"]             = env.Result.Outcome ?? "",
                        ["timestamp_ms"]        = timestampMs,
                        ["signature_id"]        = signatureId.ToString("D"),
                        ["error_message"]       = env.Result.ErrorMessage ?? "",
                        ["stack_trace"]         = env.Result.StackTrace ?? ""
                    }
                };

                await qdrant.UpsertAsync(collectionName, new List<PointStruct> { point }, wait: true);

                return Results.Ok(new IndexResponse(pointId.ToString("D"), signatureId.ToString("D")));
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
