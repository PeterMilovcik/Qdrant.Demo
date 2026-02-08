using Grpc.Core;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace FailedTests.Api.Services;

/// <summary>
/// Hosted service that ensures the Qdrant collection and payload indexes
/// exist at startup.  Retries up to 30 times (1 s apart) to tolerate the
/// case where the API container starts before Qdrant is ready.
/// </summary>
sealed class QdrantBootstrapper : BackgroundService
{
    private readonly QdrantClient _qdrant;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _collection;
    private readonly int _dim;

    public QdrantBootstrapper(
        QdrantClient qdrant,
        IHttpClientFactory httpFactory,
        string collection,
        int dim)
    {
        _qdrant     = qdrant;
        _httpFactory = httpFactory;
        _collection = collection;
        _dim        = dim;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= 30 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await EnsureCollectionAsync(stoppingToken);
                await EnsurePayloadIndexesAsync(stoppingToken);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                Console.WriteLine($"[bootstrap] attempt {attempt} failed: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    private async Task EnsureCollectionAsync(CancellationToken ct)
    {
        try
        {
            await _qdrant.CreateCollectionAsync(
                collectionName: _collection,
                vectorsConfig: new VectorParams
                {
                    Size = (uint)_dim,
                    Distance = Distance.Cosine
                },
                cancellationToken: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
        {
            // ok — collection already created on a previous run
        }
    }

    private async Task EnsurePayloadIndexesAsync(CancellationToken ct)
    {
        // Payload indexes make filtering fast on real datasets.
        // Create field indexes via REST: PUT /collections/{collection}/index

        var http = _httpFactory.CreateClient("qdrant-http");

        async Task PutIndexAsync(string fieldName, object fieldSchema)
        {
            var body = new
            {
                field_name = fieldName,
                field_schema = fieldSchema
            };

            var resp = await http.PutAsJsonAsync($"collections/{_collection}/index", body, ct);

            // If index already exists, Qdrant responds with 409; ignore.
            if (!resp.IsSuccessStatusCode && (int)resp.StatusCode != 409)
            {
                var msg = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"Index create failed for '{fieldName}': {(int)resp.StatusCode} {msg}");
            }
        }

        // keyword indexes for equality filters
        await PutIndexAsync("project_name",    "keyword");
        await PutIndexAsync("definition_name", "keyword");
        await PutIndexAsync("test_name",       "keyword");
        await PutIndexAsync("outcome",         "keyword");
        await PutIndexAsync("signature_id",    "keyword");

        // integer index for timestamp range filters
        await PutIndexAsync("timestamp_ms", new { type = "integer" });
    }
}
