using OpenAI.Embeddings;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using FailedTests.Api.Models;
using static FailedTests.Api.Helpers.TextHelpers;

namespace FailedTests.Api.Services;

/// <summary>
/// Production implementation of <see cref="ITestResultIndexer"/>.
/// Extracts the embed → build-point → upsert pipeline so it can be shared
/// across the single-result and batch-build endpoints.
/// </summary>
public sealed class TestResultIndexer : ITestResultIndexer
{
    private readonly QdrantClient _qdrant;
    private readonly EmbeddingClient _embeddings;
    private readonly string _collectionName;

    public TestResultIndexer(
        QdrantClient qdrant,
        EmbeddingClient embeddings,
        string collectionName)
    {
        _qdrant = qdrant;
        _embeddings = embeddings;
        _collectionName = collectionName;
    }

    /// <inheritdoc />
    public async Task<IndexResponse> IndexTestResultAsync(
        FailedTestEnvelope envelope,
        CancellationToken ct = default)
    {
        var testName = PickTestName(envelope.Result);

        // Deterministic point-id: idempotent per build/run/result
        var pointId = DeterministicGuid(
            $"ado|{envelope.ProjectName}|{envelope.BuildId}|{envelope.TestRunId}|{envelope.Result.Id}");

        // Deterministic signature-id: groups similar failures across builds
        var signatureId = DeterministicGuid(
            $"sig|{envelope.ProjectName}|{envelope.DefinitionName}|{testName}" +
            $"|{Normalize(envelope.Result.ErrorMessage)}|{NormalizeStack(envelope.Result.StackTrace)}");

        var embeddingText = BuildEmbeddingText(envelope, testName);
        var embedding = await _embeddings.GenerateEmbeddingAsync(embeddingText);
        var vector = embedding.Value.ToFloats().ToArray();

        var timestampMs = ToUnixMs(
            envelope.Result.CompletedDate ?? envelope.Result.StartedDate ?? DateTime.UtcNow);

        var point = new PointStruct
        {
            Id = new PointId { Uuid = pointId.ToString("D") },
            Vectors = vector,
            Payload =
            {
                ["project_name"]        = envelope.ProjectName,
                ["definition_name"]     = envelope.DefinitionName,
                ["build_id"]            = envelope.BuildId,
                ["build_name"]          = envelope.BuildName,
                ["test_run_id"]         = envelope.TestRunId,
                ["test_result_id"]      = envelope.Result.Id,
                ["test_name"]           = testName,
                ["automated_test_name"] = envelope.Result.AutomatedTestName ?? "",
                ["computer_name"]       = envelope.Result.ComputerName ?? "",
                ["outcome"]             = envelope.Result.Outcome ?? "",
                ["timestamp_ms"]        = timestampMs,
                ["signature_id"]        = signatureId.ToString("D"),
                ["error_message"]       = envelope.Result.ErrorMessage ?? "",
                ["stack_trace"]         = envelope.Result.StackTrace ?? ""
            }
        };

        await _qdrant.UpsertAsync(_collectionName, new List<PointStruct> { point }, wait: true, cancellationToken: ct);

        return new IndexResponse(pointId.ToString("D"), signatureId.ToString("D"));
    }
}
