using Qdrant.Client;
using OpenAI.Embeddings;
using FailedTests.Api.Endpoints;
using FailedTests.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- configuration from environment (docker-compose) ----
var qdrantHost     = Environment.GetEnvironmentVariable("QDRANT_HOST")       ?? "qdrant";
var qdrantHttpPort = int.Parse(Environment.GetEnvironmentVariable("QDRANT_HTTP_PORT") ?? "6333");
var qdrantGrpcPort = int.Parse(Environment.GetEnvironmentVariable("QDRANT_GRPC_PORT") ?? "6334");
var collectionName = Environment.GetEnvironmentVariable("QDRANT_COLLECTION") ?? "failed_test_results";
var embeddingDim   = int.Parse(Environment.GetEnvironmentVariable("EMBEDDING_DIM")    ?? "1536");
var openAiKey      = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY is missing");

// ---- service registration ----
builder.Services.AddSingleton(_ => new QdrantClient(qdrantHost, qdrantGrpcPort));
builder.Services.AddSingleton(_ => new EmbeddingClient("text-embedding-3-small", openAiKey));
builder.Services.AddHttpClient("qdrant-http", http =>
{
    http.BaseAddress = new Uri($"http://{qdrantHost}:{qdrantHttpPort}/");
});

builder.Services.AddHostedService(sp =>
    new QdrantBootstrapper(
        sp.GetRequiredService<QdrantClient>(),
        sp.GetRequiredService<IHttpClientFactory>(),
        collectionName,
        embeddingDim));

var app = builder.Build();

// ---- endpoints ----
app.MapGet("/", () => Results.Ok(new
{
    service = "FailedTests.Api",
    qdrant = new
    {
        host = qdrantHost,
        http = qdrantHttpPort,
        grpc = qdrantGrpcPort,
        collection = collectionName,
        embeddingDim
    }
}));

app.MapIndexEndpoints(collectionName);
app.MapSearchEndpoints(collectionName);

app.Run();
