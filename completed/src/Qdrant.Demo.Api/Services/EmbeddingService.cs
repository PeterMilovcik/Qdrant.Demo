using OpenAI.Embeddings;

namespace Qdrant.Demo.Api.Services;

/// <summary>
/// Production implementation of <see cref="IEmbeddingService"/>
/// backed by the OpenAI embeddings API.
/// </summary>
public sealed class EmbeddingService(EmbeddingClient client) : IEmbeddingService
{
    /// <inheritdoc />
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var result = await client.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return result.Value.ToFloats().ToArray();
    }
}
