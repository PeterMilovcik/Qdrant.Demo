# Module 4 — Filtered Search

> **~20 min** · Builds on [Module 3](../module-03/README.md)

## Learning objective

By the end of this module you will have:

- **Filtered** similarity search using tags (`POST /search/topk` now accepts `tags`)
- A **threshold** search endpoint that returns all results above a minimum score
- A **metadata-only** search that filters by tags without using vectors at all
- Understood the `QdrantFilterFactory` — the bridge between tag dictionaries and Qdrant filter objects

---

## Concepts introduced

### Filtered vector search

In Module 2, `/search/topk` returned the K most similar documents from the **entire** collection. Now, you can pass `tags` in the request body to narrow the search:

```json
{
  "queryText": "photosynthesis",
  "k": 3,
  "tags": { "category": "biology" }
}
```

Qdrant applies the tag filter **before** computing similarity. This means:
- Only documents with `tag.category = "biology"` are considered
- The K most similar documents **within that subset** are returned
- This is called **pre-filtering** and is very efficient

### Threshold search

`POST /search/threshold` returns **all** documents whose similarity score is ≥ a given threshold, instead of a fixed count. Use this when you want "everything relevant" rather than "the top N."

### Metadata-only search (scroll)

`POST /search/metadata` doesn't use vectors at all — it scrolls through documents that match the tag filters. This is useful for browsing/exporting a subset of your collection.

This endpoint uses Qdrant's REST API (via `HttpClient`) instead of the gRPC client, because the scroll API isn't directly exposed in the gRPC client.

### QdrantFilterFactory

The `QdrantFilterFactory` is a small service that converts a `Dictionary<string, string>` of tags into two filter formats:
- **gRPC filter** — used by the managed `QdrantClient` for vector searches
- **REST filter** — used by `HttpClient` for the scroll endpoint

Each tag becomes a `MatchKeyword` condition on the `tag.{key}` payload field. Multiple tags are combined with **AND** logic (`Must` clause).

---

## What changed from Module 3

| New file | Purpose |
|----------|---------|
| `Services/IQdrantFilterFactory.cs` | Interface for filter building |
| `Services/QdrantFilterFactory.cs` | Implementation — tags → gRPC/REST filter objects |
| `Extensions/DateTimeExtensions.cs` | `DateTime.ToUnixMs()` — cleaner than `DateTimeOffset.ToUnixTimeMilliseconds()` |
| `Tests/QdrantFilterFactoryTests.cs` | 7 tests for both gRPC and REST filter creation |
| `Tests/DateTimeExtensionsTests.cs` | 2 tests for epoch conversion |

| Changed file | What changed |
|-------------|-------------|
| `Models/Requests.cs` | Added `ThresholdSearchRequest` and `MetadataSearchRequest` records |
| `Endpoints/SearchEndpoints.cs` | Injects `IQdrantFilterFactory`; passes filter to top-K; adds `/search/threshold` and `/search/metadata` |
| `Program.cs` | Registers `IQdrantFilterFactory` and `HttpClient("qdrant-http")` |
| `Services/DocumentIndexer.cs` | Switched to `DateTime.UtcNow.ToUnixMs()` extension method |

### Code walkthrough

#### The filter factory — [`QdrantFilterFactory.cs`](src/Qdrant.Demo.Api/Services/QdrantFilterFactory.cs)

This is the bridge between the simple `Dictionary<string, string>` tags from the request body and the filter objects that Qdrant understands. Each tag becomes a `MatchKeyword` condition on the `tag.{key}` payload field, combined with AND logic:

```csharp
public Filter? CreateGrpcFilter(Dictionary<string, string>? tags)
{
    if (tags is null || tags.Count == 0) return null;

    var filter = new Filter();

    foreach (var (key, value) in tags)
    {
        filter.Must.Add(MatchKeyword($"tag.{key}", value));
    }

    return filter;
}
```

Returning `null` when there are no tags means "no filter" — the search considers all documents. The factory also has a `CreateScrollFilter` method that builds the equivalent filter as an anonymous object for the REST scroll API.

#### Filtered top-K search — [`SearchEndpoints.cs`](src/Qdrant.Demo.Api/Endpoints/SearchEndpoints.cs)

The top-K endpoint now injects `IQdrantFilterFactory` and passes the filter to `qdrant.SearchAsync`:

```csharp
var vector = await embeddings.EmbedAsync(req.QueryText, ct);
var filter = filters.CreateGrpcFilter(req.Tags);

var hits = await qdrant.SearchAsync(
    collectionName: collectionName,
    vector: vector,
    limit: (ulong)req.K,
    filter: filter,
    payloadSelector: true,
    cancellationToken: ct);
```

Qdrant applies the filter **before** similarity — only matching documents are scored.

#### Threshold search — [`SearchEndpoints.cs`](src/Qdrant.Demo.Api/Endpoints/SearchEndpoints.cs)

Instead of a fixed K, the threshold endpoint passes a `scoreThreshold` parameter. Qdrant returns all points whose cosine similarity is at or above the threshold:

```csharp
var hits = await qdrant.SearchAsync(
    collectionName: collectionName,
    vector: vector,
    limit: (ulong)req.Limit,
    filter: filter,
    scoreThreshold: req.ScoreThreshold,
    payloadSelector: true,
    cancellationToken: ct);
```

The `Limit` parameter (default 100) acts as a safety cap to prevent unbounded results.

#### Metadata-only scroll — [`SearchEndpoints.cs`](src/Qdrant.Demo.Api/Endpoints/SearchEndpoints.cs)

The metadata endpoint doesn't use vectors at all — it calls Qdrant's REST scroll API via `HttpClient`:

```csharp
var http = httpFactory.CreateClient("qdrant-http");
var filter = filters.CreateScrollFilter(req.Tags);

var body = new
{
    limit = req.Limit,
    with_payload = true,
    with_vector = false,
    filter
};

var resp = await http.PostAsJsonAsync(
    $"collections/{collectionName}/points/scroll", body, ct);
```

`with_vector = false` keeps the response small — you only get point ids and payloads, not the full 1536-float vectors.

---

## Step 1 — Start Qdrant and run the API

```bash
cd module-04
docker compose up -d    # starts Qdrant (http://localhost:6333)
```

Then run the API locally:

```bash
cd src/Qdrant.Demo.Api
```

```powershell
# PowerShell
$env:ASPNETCORE_URLS = "http://localhost:8080"
```

```bash
# Linux/macOS
export ASPNETCORE_URLS="http://localhost:8080"
```

```bash
dotnet run
```

## Step 2 — Index documents with tags

```bash
curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "bio-001",
    "text": "Photosynthesis converts sunlight into chemical energy in green plants.",
    "tags": { "category": "biology", "level": "introductory" }
  }'

curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "phys-001",
    "text": "Quantum entanglement links two particles across any distance.",
    "tags": { "category": "physics", "level": "advanced" }
  }'

curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "bio-002",
    "text": "DNA replication is the biological process of producing two identical copies of DNA.",
    "tags": { "category": "biology", "level": "intermediate" }
  }'
```

## Step 3 — Filtered top-K search

Search for "energy" but only in biology documents:

```bash
curl -X POST http://localhost:8080/search/topk \
  -H "Content-Type: application/json" \
  -d '{
    "queryText": "energy",
    "k": 5,
    "tags": { "category": "biology" }
  }'
```

The physics document is **excluded** even though "energy" might be semantically relevant to it.

## Step 4 — Threshold search

Return all documents with similarity ≥ 0.4:

```bash
curl -X POST http://localhost:8080/search/threshold \
  -H "Content-Type: application/json" \
  -d '{"queryText": "biological processes", "scoreThreshold": 0.4}'
```

## Step 5 — Metadata-only search

Browse all biology documents (no vector search involved):

```bash
curl -X POST http://localhost:8080/search/metadata \
  -H "Content-Type: application/json" \
  -d '{"tags": { "category": "biology" }}'
```

---

## Exercises

### Exercise 4.1 — Combine tags

Filter by two tags at once: `"category": "biology"` AND `"level": "introductory"`. Only the photosynthesis document should match.

### Exercise 4.2 — Empty filter

Call `/search/topk` without tags — it should behave like Module 2 (return all documents ranked by similarity).

### Exercise 4.3 — Tune the threshold

Try `/search/threshold` with `"scoreThreshold": 0.8` — most results will be filtered out. Then try `0.2` — you'll get almost everything. Find a sweet spot for your data.

### Exercise 4.4 — Inspect the filter factory

Open `QdrantFilterFactoryTests.cs` and read through the tests. Notice how null/empty tags produce `null` filters (meaning "no filter"), while populated tags produce `Must` conditions.

### Exercise 4.5 — Run the tests

```bash
cd module-04
dotnet test
```

You should see **20 tests passed**.

---

## ✅ Checkpoint

At this point you have:

- [x] Three search strategies: top-K, threshold, metadata-only
- [x] Tag filtering on all search endpoints
- [x] `QdrantFilterFactory` converting tag dictionaries to Qdrant filter objects
- [x] Understanding of: pre-filtering, threshold search, scroll API, gRPC vs REST filters

## 🧹 Clean Up

Before moving to the next module, stop everything started in this module:

1. **Stop the local API** — press `Ctrl+C` in the terminal where `dotnet run` is running
2. **Stop Docker containers** — from the `module-04` directory:

```bash
docker compose down
```

This stops Qdrant so the next module starts fresh.

**Next →** [Module 5 — RAG Chat](../module-05/README.md)
