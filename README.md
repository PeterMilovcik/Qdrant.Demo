Below is a full tutorial that runs Qdrant **without a Qdrant API key**, inside a **Docker Compose private network**, with a **.NET 8 Web API** that uses **OpenAI Embeddings** and can:

* **Upsert** (write) embeddings + payload (metadata)
* **Vector search** with optional metadata filters
* **Metadata-only search** (no vector) using Qdrant's scroll API
* Use a **deterministic UUID point-id** so reprocessing the same Azure DevOps test result is idempotent

---

## Prerequisites

| Tool | Version | Why |
|------|---------|-----|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | 4.x+ | Runs Qdrant + API containers |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0+ | Only needed if you want to build/debug locally outside Docker |
| [OpenAI API key](https://platform.openai.com/api-keys) | — | Used for `text-embedding-3-small` embeddings (1 536 dims) |
| `curl` or similar HTTP client | — | To test the API endpoints |

---

## 1) Folder layout

```
Qdrant.Demo/
  .gitignore
  docker-compose.yml
  README.md
  src/
    FailedTests.Api/
      .dockerignore
      Dockerfile
      FailedTests.Api.csproj
      Program.cs                          # thin — config, DI, endpoint registration
      Endpoints/
        IndexEndpoints.cs                 # POST /index/test-result
        SearchEndpoints.cs                # POST /search/similar + /search/metadata
      Helpers/
        TextHelpers.cs                    # embedding text, normalisation, deterministic GUID
        QdrantPayloadHelpers.cs           # gRPC Value → CLR, scroll-filter builder
      Models/
        Requests.cs                       # all DTOs / request-response records
      Services/
        QdrantBootstrapper.cs             # BackgroundService — collection + index setup
```

---

## 2) docker-compose.yml (no API key, private network)

Qdrant's REST port (6333) is published to the host so you can browse the **Qdrant Dashboard** at `http://localhost:6333/dashboard` during development. gRPC (6334) stays internal — only the API container needs it.

> ⚠️ **Local dev only.** Port 6333 is published without an API key. In production, either keep Qdrant on an internal network or configure a [Qdrant API key](https://qdrant.tech/documentation/guides/security/).

```yaml
services:
  qdrant:
    image: qdrant/qdrant:v1.16.3
    volumes:
      - qdrant_storage:/qdrant/storage
    expose:
      - "6334" # gRPC (internal only)
    ports:
      - "6333:6333" # REST + dashboard (accessible at http://localhost:6333/dashboard)
    networks:
      - backend

  failedtests-api:
    build:
      context: ./src/FailedTests.Api
      dockerfile: Dockerfile
    environment:
      ASPNETCORE_URLS: http://+:8080

      # Qdrant endpoints inside the compose network
      QDRANT_HOST: qdrant
      QDRANT_HTTP_PORT: "6333"
      QDRANT_GRPC_PORT: "6334"

      # OpenAI key (passed from host env)
      OPENAI_API_KEY: ${OPENAI_API_KEY}

      # App settings
      QDRANT_COLLECTION: failed_test_results
      EMBEDDING_DIM: "1536"
    depends_on:
      - qdrant
    ports:
      - "8080:8080"  # expose ONLY the API to your machine
    networks:
      - backend

volumes:
  qdrant_storage:

networks:
  backend:
    driver: bridge
```

Notes:

* Qdrant uses **REST on 6333** (also dashboard at `/dashboard`) and **gRPC on 6334**. ([qdrant.tech][1])
* The Docker image tag above is a pinned version for reproducibility. ([GitHub][2])
* `depends_on` ensures Docker starts Qdrant before the API. The `QdrantBootstrapper` hosted service handles retries if Qdrant is still initialising when the API container starts.
* Browse **http://localhost:6333/dashboard** to inspect collections, points, and run ad-hoc queries.

---

## 3) Create the .NET 8 API project

From `./Qdrant.Demo/src`:

```bash
dotnet new webapi -n FailedTests.Api
```

Add the NuGet packages (pinning versions for reproducibility):

```bash
cd FailedTests.Api
dotnet add package Qdrant.Client --version 1.16.1
dotnet add package OpenAI --version 2.8.0
```

The Qdrant .NET SDK connects to Qdrant over **gRPC** (typical local default shown in their repo docs). ([GitHub][3])
The [OpenAI .NET SDK](https://github.com/openai/openai-dotnet) provides the `EmbeddingClient` used for generating embeddings.

### FailedTests.Api.csproj (for reference)

After adding packages, your `.csproj` should look like this:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Qdrant.Client" Version="1.16.1" />
    <PackageReference Include="OpenAI" Version="2.8.0" />
  </ItemGroup>

</Project>
```

---

## 4) Dockerfile + .dockerignore

### `./src/FailedTests.Api/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "FailedTests.Api.dll"]
```

### `./src/FailedTests.Api/.dockerignore`

Keeps `bin/` and `obj/` out of the Docker build context (faster builds, smaller images):

```
bin/
obj/
.git
.gitignore
*.md
*.sln
.vs/
.vscode/
.idea/
```

---

## 5) Deterministic point-id strategy (why + how)

Qdrant point IDs can be **either uint64 or UUID**. ([qdrant.tech][4])
Qdrant "upsert" will overwrite the same point ID (that's exactly what we want for idempotent reprocessing). ([api.qdrant.tech][5])

### Strategy used in this tutorial

Use a deterministic UUID from the stable Azure DevOps identity tuple:

```
pointId = UUID( "ado|{project}|{buildId}|{testRunId}|{testResultId}" )
```

So if your ingestion job replays the same run/result again, you update the same point instead of inserting duplicates.

> Optional (recommended): also compute a separate `signature_id` in payload (deterministic hash of normalized error+stack) to group similar failures across runs. You'll see it in code.

---

## 6) Azure DevOps TestCaseResult DTO (shape aligned to SDK)

In Azure DevOps .NET SDK, `TestCaseResult` includes fields like `Id`, `ComputerName`, `ErrorMessage`, `StackTrace`, `TestCaseTitle`, `AutomatedTestName`, etc. ([learn.microsoft.com][6])

In your Web API you'll usually map the SDK object into your own DTO/envelope anyway (versioning, smaller payloads, etc.). We'll do that.

---

## 7) Application code (multi-file layout)

The code is organised following **.NET minimal API best practices**:

| File | Responsibility |
|------|---------------|
| `Program.cs` | Thin entry point — configuration, DI, endpoint registration |
| `Endpoints/IndexEndpoints.cs` | `POST /index/test-result` — upsert logic |
| `Endpoints/SearchEndpoints.cs` | `POST /search/similar` + `POST /search/metadata` |
| `Helpers/TextHelpers.cs` | Embedding text, normalisation, deterministic GUID, timestamps |
| `Helpers/QdrantPayloadHelpers.cs` | gRPC `Value` → CLR conversion, scroll-filter builder |
| `Models/Requests.cs` | All DTOs / request-response records |
| `Services/QdrantBootstrapper.cs` | `BackgroundService` — collection + payload-index bootstrap |

> **⚠️ Qdrant.Client SDK gotchas (important for AI agents and humans alike):**
>
> 1. **`Range` type ambiguity**: `Range` in Qdrant.Client collides with `System.Range`. Always fully qualify as `Qdrant.Client.Grpc.Range`. The `Gte`/`Lte` properties are `double` (not `double?`), so use conditional assignment.
> 2. **`SearchAsync` parameters**: Use `payloadSelector: true` (not `withPayload`). The `limit` parameter is `ulong` (not `uint`). Use `scoreThreshold: float?` to cut off low-similarity noise. The `filter` parameter expects `Filter?`, not `Condition?` — wrap conditions: `new Filter { Must = { condition } }`.
> 3. **Payload `Value` type**: Qdrant.Client uses `Qdrant.Client.Grpc.Value` (not `Google.Protobuf.WellKnownTypes.Value`). Use `KindOneofCase.DoubleValue` and `IntegerValue` (there is no `NumberValue`).

### `Program.cs` — thin entry point

```csharp
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
```

### `Endpoints/IndexEndpoints.cs`

```csharp
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

                var pointId = DeterministicGuid(
                    $"ado|{env.ProjectName}|{env.BuildId}|{env.TestRunId}|{env.Result.Id}");

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
```

### `Endpoints/SearchEndpoints.cs`

```csharp
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
        // Vector similarity search + optional metadata filters
        app.MapPost("/search/similar", async (
            [FromBody] SimilaritySearchRequest req,
            QdrantClient qdrant,
            EmbeddingClient embeddings) =>
        {
            try
            {
                var embedding = await embeddings.GenerateEmbeddingAsync(req.QueryText);
                var vector = embedding.Value.ToFloats().ToArray();

                Condition? filter = null;

                if (!string.IsNullOrWhiteSpace(req.ProjectName))
                    filter = MatchKeyword("project_name", req.ProjectName);

                if (!string.IsNullOrWhiteSpace(req.DefinitionName))
                    filter = filter is null
                        ? MatchKeyword("definition_name", req.DefinitionName)
                        : filter & MatchKeyword("definition_name", req.DefinitionName);

                if (req.FromTimestampMs is not null || req.ToTimestampMs is not null)
                {
                    var range = new Qdrant.Client.Grpc.Range();
                    if (req.FromTimestampMs is not null) range.Gte = (double)req.FromTimestampMs.Value;
                    if (req.ToTimestampMs is not null)   range.Lte = (double)req.ToTimestampMs.Value;
                    var timeCond = Range("timestamp_ms", range);
                    filter = filter is null ? timeCond : filter & timeCond;
                }

                var searchFilter = filter is null ? null : new Filter { Must = { filter } };

                var hits = await qdrant.SearchAsync(
                    collectionName: collectionName,
                    vector: vector,
                    limit: (ulong)req.Limit,
                    filter: searchFilter,
                    scoreThreshold: req.ScoreThreshold,
                    payloadSelector: true);

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

        // Metadata-only search (no vector) via Qdrant REST scroll
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
```

### `Helpers/TextHelpers.cs`

```csharp
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FailedTests.Api.Models;

namespace FailedTests.Api.Helpers;

public static class TextHelpers
{
    public static string PickTestName(AzureDevOpsTestCaseResultDto r)
        => !string.IsNullOrWhiteSpace(r.AutomatedTestName) ? r.AutomatedTestName!
         : !string.IsNullOrWhiteSpace(r.TestCaseTitle)     ? r.TestCaseTitle!
         : "<unknown-test>";

    public static string BuildEmbeddingText(FailedTestEnvelope env, string testName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Project: {env.ProjectName}");
        sb.AppendLine($"Definition: {env.DefinitionName}");
        sb.AppendLine($"Build: {env.BuildName} ({env.BuildId})");
        sb.AppendLine($"Test: {testName}");
        sb.AppendLine($"Outcome: {env.Result.Outcome}");
        sb.AppendLine();
        sb.AppendLine(env.Result.ErrorMessage ?? "");
        sb.AppendLine();
        sb.AppendLine(env.Result.StackTrace ?? "");
        return sb.ToString();
    }

    public static Guid DeterministicGuid(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        Span<byte> g = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(g);
        g[6] = (byte)((g[6] & 0x0F) | 0x50); // version 5
        g[8] = (byte)((g[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(g);
    }

    public static long ToUnixMs(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        return (long)(utc - DateTime.UnixEpoch).TotalMilliseconds;
    }

    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var text = s.Trim();
        text = Regex.Replace(text,
            @"\b[0-9a-fA-F]{8}\b-[0-9a-fA-F]{4}\b-[0-9a-fA-F]{4}\b-[0-9a-fA-F]{4}\b-[0-9a-fA-F]{12}\b",
            "<guid>");
        text = Regex.Replace(text, @"\b\d+\b", "<n>");
        text = Regex.Replace(text, @"\s+", " ");
        return text;
    }

    public static string NormalizeStack(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var lines = s.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                     .Select(l => l.Trim())
                     .Select(l => Regex.Replace(l, @":line\s+\d+", ":line <n>"))
                     .Take(12);
        return string.Join("\n", lines);
    }
}
```

### `Helpers/QdrantPayloadHelpers.cs`

```csharp
using FailedTests.Api.Models;

namespace FailedTests.Api.Helpers;

public static class QdrantPayloadHelpers
{
    public static Dictionary<string, object?> PayloadToDictionary(
        IDictionary<string, Qdrant.Client.Grpc.Value> payload)
    {
        return payload.ToDictionary(kv => kv.Key, kv => FromProto(kv.Value));
    }

    public static object? BuildScrollFilter(MetadataSearchRequest req)
    {
        var must = new List<object>();

        void AddMatch(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            must.Add(new { key, match = new { value } });
        }

        AddMatch("project_name", req.ProjectName);
        AddMatch("definition_name", req.DefinitionName);
        AddMatch("test_name", req.TestName);
        AddMatch("outcome", req.Outcome);

        if (req.FromTimestampMs is not null || req.ToTimestampMs is not null)
        {
            must.Add(new
            {
                key = "timestamp_ms",
                range = new { gte = req.FromTimestampMs, lte = req.ToTimestampMs }
            });
        }

        if (must.Count == 0) return null;
        return new { must };
    }

    private static object? FromProto(Qdrant.Client.Grpc.Value v) =>
        v.KindCase switch
        {
            Qdrant.Client.Grpc.Value.KindOneofCase.StringValue  => v.StringValue,
            Qdrant.Client.Grpc.Value.KindOneofCase.DoubleValue  => v.DoubleValue,
            Qdrant.Client.Grpc.Value.KindOneofCase.IntegerValue => v.IntegerValue,
            Qdrant.Client.Grpc.Value.KindOneofCase.BoolValue    => v.BoolValue,
            Qdrant.Client.Grpc.Value.KindOneofCase.StructValue  =>
                v.StructValue.Fields.ToDictionary(f => f.Key, f => FromProto(f.Value)),
            Qdrant.Client.Grpc.Value.KindOneofCase.ListValue    =>
                v.ListValue.Values.Select(FromProto).ToList(),
            _ => null
        };
}
```

### `Models/Requests.cs`

```csharp
namespace FailedTests.Api.Models;

public record AzureDevOpsTestCaseResultDto(
    int Id,
    string? TestCaseTitle,
    string? AutomatedTestName,
    string? ComputerName,
    string? Outcome,
    string? ErrorMessage,
    string? StackTrace,
    DateTime? StartedDate,
    DateTime? CompletedDate
);

public record FailedTestEnvelope(
    string ProjectName,
    string DefinitionName,
    int BuildId,
    string BuildName,
    int TestRunId,
    AzureDevOpsTestCaseResultDto Result
);

public record IndexResponse(string PointId, string SignatureId);

public record SimilaritySearchRequest(
    string QueryText,
    float ScoreThreshold = 0.42f,
    int Limit = 100,
    string? ProjectName = null,
    string? DefinitionName = null,
    long? FromTimestampMs = null,
    long? ToTimestampMs = null
);

public record MetadataSearchRequest(
    int Limit = 25,
    string? ProjectName = null,
    string? DefinitionName = null,
    string? TestName = null,
    string? Outcome = null,
    long? FromTimestampMs = null,
    long? ToTimestampMs = null
);
```

### `Services/QdrantBootstrapper.cs`

```csharp
using Grpc.Core;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace FailedTests.Api.Services;

sealed class QdrantBootstrapper : BackgroundService
{
    private readonly QdrantClient _qdrant;
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _collection;
    private readonly int _dim;

    public QdrantBootstrapper(
        QdrantClient qdrant, IHttpClientFactory httpFactory, string collection, int dim)
    {
        _qdrant = qdrant; _httpFactory = httpFactory;
        _collection = collection; _dim = dim;
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
                vectorsConfig: new VectorParams { Size = (uint)_dim, Distance = Distance.Cosine },
                cancellationToken: ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists) { }
    }

    private async Task EnsurePayloadIndexesAsync(CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("qdrant-http");

        async Task PutIndexAsync(string fieldName, object fieldSchema)
        {
            var body = new { field_name = fieldName, field_schema = fieldSchema };
            var resp = await http.PutAsJsonAsync($"collections/{_collection}/index", body, ct);
            if (!resp.IsSuccessStatusCode && (int)resp.StatusCode != 409)
            {
                var msg = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(
                    $"Index create failed for '{fieldName}': {(int)resp.StatusCode} {msg}");
            }
        }

        await PutIndexAsync("project_name",    "keyword");
        await PutIndexAsync("definition_name", "keyword");
        await PutIndexAsync("test_name",       "keyword");
        await PutIndexAsync("outcome",         "keyword");
        await PutIndexAsync("signature_id",    "keyword");
        await PutIndexAsync("timestamp_ms", new { type = "integer" });
    }
}
```

What you got here:

* **Write path:** `/index/test-result` upserts a point with vector + payload
* **Similarity search:** `/search/similar` uses Qdrant vector search with a **score threshold** (default 0.42) to return all meaningful matches, plus optional metadata filters
* **Metadata-only search:** `/search/metadata` uses Qdrant scroll + filter + range
* **Error handling:** All endpoints return structured `ProblemDetails` on failure (instead of bare 500s)

---

## 8) Run it

First, export your OpenAI API key on the host so Docker Compose can pick it up:

```bash
# Linux / macOS
export OPENAI_API_KEY="sk-..."

# Windows PowerShell
$env:OPENAI_API_KEY = "sk-..."
```

Then from `./Qdrant.Demo`:

```bash
docker compose up --build
```

API should be reachable at:

* `http://localhost:8080/`

You should see bootstrap logs like:

```
failedtests-api-1  | info: Microsoft.Hosting.Lifetime[14]
failedtests-api-1  |       Now listening on: http://[::]:8080
```

---

## 9) Test with sample payloads

### 9.1 Index a failed test result

```bash
curl -X POST http://localhost:8080/index/test-result \
  -H "Content-Type: application/json" \
  -d '{
    "projectName": "MyProject",
    "definitionName": "CI_Main",
    "buildId": 12345,
    "buildName": "CI_Main_2026.02.04",
    "testRunId": 777,
    "result": {
      "id": 42,
      "testCaseTitle": "Should_calculate_totals",
      "automatedTestName": "My.Tests.CalculatorTests.Should_calculate_totals",
      "computerName": "agent-12",
      "outcome": "Failed",
      "errorMessage": "System.NullReferenceException: Object reference not set to an instance of an object",
      "stackTrace": "at My.App.Calculator.Add(Int32 a, Int32 b) in C:\\src\\Calculator.cs:line 88\nat My.Tests.CalculatorTests.Should_calculate_totals() in C:\\src\\CalculatorTests.cs:line 34",
      "startedDate": "2026-02-04T10:00:00Z",
      "completedDate": "2026-02-04T10:00:02Z"
    }
  }'
```

Expected response:

```json
{
  "pointId": "50f86b54-...",
  "signatureId": "9a1c3e7f-..."
}
```

Re-indexing the same result returns the **same `pointId`** (idempotent upsert).

### 9.2 Similarity search (optionally filter by project/definition)

```bash
curl -X POST http://localhost:8080/search/similar \
  -H "Content-Type: application/json" \
  -d '{
    "queryText": "NullReferenceException at Calculator.Add",
    "scoreThreshold": 0.42,
    "projectName": "MyProject",
    "definitionName": "CI_Main"
  }'
```

Expected response — an array ranked by cosine similarity score:

```json
[
  {
    "id": "50f86b54-...",
    "score": 0.58,
    "payload": {
      "project_name": "MyProject",
      "test_name": "My.Tests.CalculatorTests.Should_calculate_totals",
      "error_message": "System.NullReferenceException: ...",
      "..."
    }
  }
]
```

### 9.3 Metadata-only search (no vectors)

```bash
curl -X POST http://localhost:8080/search/metadata \
  -H "Content-Type: application/json" \
  -d '{
    "limit": 25,
    "projectName": "MyProject",
    "outcome": "Failed",
    "fromTimestampMs": 1707040000000,
    "toTimestampMs": 1890000000000
  }'
```

Expected response — Qdrant scroll result with points and payloads:

```json
{
  "result": {
    "points": [
      {
        "id": { "uuid": "50f86b54-..." },
        "payload": { "project_name": "MyProject", "..." }
      }
    ],
    "next_page_offset": null
  },
  "status": "ok",
  "time": 0.001
}
```

---

## 10) Side note: when an API key is useful

You **don't need** an API key if Qdrant stays inside a private Docker network and only trusted services can reach it.

You typically **do** want an API key when:

* Qdrant is exposed outside your internal network (ports published, VM, Kubernetes ingress, cloud)
* Multiple teams/services share a cluster
* You want auth-based protection (including signed tokens)

Qdrant supports configuring an API key in its security configuration. ([qdrant.tech][7])

---

## 11) Switching Embedding Models

This tutorial uses OpenAI's `text-embedding-3-small` (1 536 dims). To use a different model:

1. Update `EMBEDDING_DIM` in `docker-compose.yml`.
2. Configure `EmbeddingClient` with your preferred model or provider (e.g. Azure OpenAI, local models).
3. Rebuild the container.

---

## 12) Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `OPENAI_API_KEY is missing` at startup | Env var not exported before `docker compose up` | Run `export OPENAI_API_KEY="sk-..."` (Linux/Mac) or `$env:OPENAI_API_KEY = "sk-..."` (PowerShell) first |
| `docker compose up` hangs or qdrant never becomes healthy | Docker Desktop not running | Start Docker Desktop and wait for the engine to be ready |
| `[bootstrap] attempt N failed: ...` repeating 30 times | Qdrant container didn't start in time | Check `docker compose logs qdrant` for errors; ensure port 6333/6334 are free |
| `CS0104: 'Range' is an ambiguous reference` | `System.Range` vs `Qdrant.Client.Grpc.Range` | Use fully qualified `Qdrant.Client.Grpc.Range` |
| Similarity search returns `[]` | No points indexed yet, or filter too restrictive | Index a point first, try searching without filters |
| 500 error with `AuthenticationException` from OpenAI | Invalid or expired API key | Verify your key at [platform.openai.com](https://platform.openai.com/api-keys) |

---

## Next steps

* A `QdrantFailureStore` service class (DI-friendly) to encapsulate Qdrant operations behind an interface
* A "bucket collection" approach (signature-level points + occurrence collection) to support both "latest summary" and "full history" cleanly

[1]: https://qdrant.tech/documentation/quickstart/ "https://qdrant.tech/documentation/quickstart/"
[2]: https://github.com/qdrant/qdrant/releases "https://github.com/qdrant/qdrant/releases"
[3]: https://github.com/qdrant/qdrant-dotnet "https://github.com/qdrant/qdrant-dotnet"
[4]: https://qdrant.tech/documentation/concepts/points/ "https://qdrant.tech/documentation/concepts/points/"
[5]: https://api.qdrant.tech/v-1-12-x/api-reference/points/upsert-points "https://api.qdrant.tech/v-1-12-x/api-reference/points/upsert-points"
[6]: https://learn.microsoft.com/en-us/dotnet/api/microsoft.teamfoundation.testmanagement.webapi.testcaseresult?view=azure-devops-dotnet "https://learn.microsoft.com/en-us/dotnet/api/microsoft.teamfoundation.testmanagement.webapi.testcaseresult?view=azure-devops-dotnet"
[7]: https://qdrant.tech/documentation/guides/security/ "https://qdrant.tech/documentation/guides/security/"