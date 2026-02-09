using Qdrant.Client;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Qdrant.Demo.Api.Endpoints;
using Qdrant.Demo.Api.Models;
using Qdrant.Demo.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- configuration (appsettings.json → env vars override) ----
var config = builder.Configuration;

var qdrantHost     = config["QDRANT_HOST"]       ?? config["Qdrant:Host"]       ?? "localhost";
var qdrantHttpPort = int.Parse(config["QDRANT_HTTP_PORT"] ?? config["Qdrant:HttpPort"] ?? "6333");
var qdrantGrpcPort = int.Parse(config["QDRANT_GRPC_PORT"] ?? config["Qdrant:GrpcPort"] ?? "6334");
var collectionName = config["QDRANT_COLLECTION"] ?? config["Qdrant:Collection"] ?? "documents";
var embeddingDim   = int.Parse(config["EMBEDDING_DIM"]    ?? config["Qdrant:EmbeddingDim"] ?? "1536");
var embeddingModel = config["OPENAI_EMBEDDING_MODEL"] ?? config["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
var chatModel      = config["OPENAI_CHAT_MODEL"]      ?? config["OpenAI:ChatModel"]      ?? "gpt-4.1-nano";
var openAiKey      = config["OPENAI_API_KEY"]
    ?? throw new InvalidOperationException("OPENAI_API_KEY is missing");

// ---- chunking options ----
var chunkingOptions = new ChunkingOptions();
config.GetSection("Chunking").Bind(chunkingOptions);
var maxChunk = config["CHUNKING_MAX_SIZE"];
var overlapCfg = config["CHUNKING_OVERLAP"];
if (maxChunk is not null) chunkingOptions.MaxChunkSize = int.Parse(maxChunk);
if (overlapCfg is not null) chunkingOptions.Overlap = int.Parse(overlapCfg);

// ---- service registration ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new() { Title = "Qdrant.Demo API", Version = "v1" });
});

builder.Services.AddSingleton(_ => new QdrantClient(qdrantHost, qdrantGrpcPort));
builder.Services.AddSingleton(_ => new EmbeddingClient(embeddingModel, openAiKey));
builder.Services.AddSingleton(_ => new ChatClient(chatModel, openAiKey));
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddSingleton<IQdrantFilterFactory, QdrantFilterFactory>();
builder.Services.AddSingleton(chunkingOptions);
builder.Services.AddSingleton<ITextChunker, TextChunker>();
builder.Services.AddHttpClient("qdrant-http", http =>
{
    http.BaseAddress = new Uri($"http://{qdrantHost}:{qdrantHttpPort}/");
});

builder.Services.AddHostedService(sp =>
    new QdrantBootstrapper(
        sp.GetRequiredService<QdrantClient>(),
        collectionName,
        embeddingDim));

builder.Services.AddSingleton<IDocumentIndexer>(sp =>
    new DocumentIndexer(
        sp.GetRequiredService<QdrantClient>(),
        sp.GetRequiredService<IEmbeddingService>(),
        sp.GetRequiredService<ITextChunker>(),
        collectionName));

var app = builder.Build();

// ---- Swagger (always-on — this is a workshop template) ----
app.UseSwagger();
app.UseSwaggerUI();

// ---- endpoints ----
app.MapGet("/", () => Results.Ok(new
{
    service = "Qdrant.Demo.Api",
    qdrant = new
    {
        host = qdrantHost,
        http = qdrantHttpPort,
        grpc = qdrantGrpcPort,
        collection = collectionName,
        embeddingDim
    },
    embeddingModel,
    chatModel,
    chunking = new
    {
        maxChunkSize = chunkingOptions.MaxChunkSize,
        overlap = chunkingOptions.Overlap
    }
}));

app.MapGet("/health", () => Results.Ok("healthy"))
    .ExcludeFromDescription();

app.MapDocumentEndpoints();
app.MapSearchEndpoints(collectionName);
app.MapChatEndpoints(collectionName);

app.Run();
