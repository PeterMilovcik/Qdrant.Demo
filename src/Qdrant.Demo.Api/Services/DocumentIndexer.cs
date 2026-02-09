using Qdrant.Client;
using Qdrant.Client.Grpc;
using Qdrant.Demo.Api.Extensions;
using Qdrant.Demo.Api.Models;
using static Qdrant.Demo.Api.Models.PayloadKeys;

namespace Qdrant.Demo.Api.Services;

/// <summary>
/// Production implementation of <see cref="IDocumentIndexer"/>.
/// Embeds the document text via OpenAI, stores tags as <c>tag.{key}</c>
/// and properties as <c>prop.{key}</c> in the Qdrant payload.
/// </summary>
public sealed class DocumentIndexer(
    QdrantClient qdrant,
    IEmbeddingService embeddings,
    string collectionName) : IDocumentIndexer
{
    /// <inheritdoc />
    public async Task<DocumentUpsertResponse> IndexAsync(
        DocumentUpsertRequest request,
        CancellationToken ct = default)
    {
        // Deterministic point-id: from caller-supplied Id, or hash of Text
        var idSource = !string.IsNullOrWhiteSpace(request.Id)
            ? request.Id!
            : request.Text;
        var pointId = idSource.ToDeterministicGuid();

        // Generate embedding from the document text
        var vector = await embeddings.EmbedAsync(request.Text, ct);

        var point = new PointStruct
        {
            Id = new PointId { Uuid = pointId.ToString("D") },
            Vectors = vector,
            Payload =
            {
                // Store the original text so it can be returned in results
                [Text] = request.Text,
                // Timestamp for when this point was indexed
                [IndexedAtMs] = DateTime.UtcNow.ToUnixMs()
            }
        };

        // Store tags as tag.{key} — these are indexed and filterable
        if (request.Tags is not null)
        {
            foreach (var (key, value) in request.Tags)
                point.Payload[$"{TagPrefix}{key}"] = value;
        }

        // Store properties as prop.{key} — informational only, not indexed
        if (request.Properties is not null)
        {
            foreach (var (key, value) in request.Properties)
                point.Payload[$"{PropertyPrefix}{key}"] = value;
        }

        await qdrant.UpsertAsync(
            collectionName,
            [point],
            wait: true,
            cancellationToken: ct);

        return new DocumentUpsertResponse(pointId.ToString("D"));
    }
}
