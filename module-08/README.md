# Module 8 — Batch Operations

> **~10 min** · Builds on [Module 7](../module-07/README.md)

## Learning objective

By the end of this module you will have:

- A `POST /documents/batch` endpoint for indexing multiple documents in one API call
- Understood partial-failure handling (some documents can fail while others succeed)
- Completed the full workshop — congratulations! 🎉

---

## Concepts introduced

### Batch indexing

Until now, indexing one document required one HTTP request. With hundreds or thousands of documents, that means hundreds of round-trips.

`POST /documents/batch` accepts an **array** of `DocumentUpsertRequest` objects and indexes them sequentially in a single request. The response reports how many succeeded and which ones failed (with error messages).

### Partial failure

The batch endpoint doesn't stop at the first error. If document #3 out of 10 fails (e.g. empty text), documents #1-2 and #4-10 still get indexed. The `BatchUpsertResponse` gives you a full accounting:

```json
{
  "total": 10,
  "succeeded": 9,
  "failed": 1,
  "errors": ["[bad-doc]: Text is required and cannot be empty."]
}
```

---

## What changed from Module 7

| Changed file | What changed |
|-------------|-------------|
| `Models/Requests.cs` | Added `BatchUpsertResponse` record |
| `Endpoints/DocumentEndpoints.cs` | Added `POST /documents/batch` — loops through the array, calls `IndexAsync` per item, collects results |

No new services, no new tests — this is a thin endpoint layer.

### Code walkthrough

#### Batch response model — [`Requests.cs`](src/Qdrant.Demo.Api/Models/Requests.cs)

A simple accounting record that tells the caller exactly what happened:

```csharp
public record BatchUpsertResponse(
    int Total,
    int Succeeded,
    int Failed,
    IReadOnlyList<string> Errors
);
```

#### The batch endpoint — [`DocumentEndpoints.cs`](src/Qdrant.Demo.Api/Endpoints/DocumentEndpoints.cs)

The endpoint accepts an array of `DocumentUpsertRequest` and processes each one independently. Failures are collected, not thrown — so one bad document doesn't block the rest:

```csharp
app.MapPost("/documents/batch", async (
    [FromBody] IReadOnlyList<DocumentUpsertRequest> batch,
    IDocumentIndexer indexer,
    CancellationToken ct) =>
{
    List<string> errors = [];
    var succeeded = 0;

    foreach (var req in batch)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
        {
            var label = req.Id ?? "(empty)";
            errors.Add($"[{label}]: Text is required and cannot be empty.");
            continue;
        }

        try
        {
            await indexer.IndexAsync(req, ct);
            succeeded++;
        }
        catch (Exception ex)
        {
            var label = req.Id ?? req.Text[..Math.Min(req.Text.Length, 40)];
            errors.Add($"[{label}]: {ex.Message}");
        }
    }

    return Results.Ok(new BatchUpsertResponse(
        Total: batch.Count,
        Succeeded: succeeded,
        Failed: errors.Count,
        Errors: errors));
});
```

The error label uses the document's `Id` when available, or the first 40 characters of the text as a fallback — making it easy to identify which item failed in a large batch.

---

## Step 1 — Start Qdrant and run the API

```bash
cd module-08
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

## Step 2 — Batch index several documents

1. Open **Swagger UI** in your browser: **http://localhost:8080/swagger**
2. Find the **POST /documents/batch** endpoint, click **Try it out**
3. Paste the following JSON array and click **Execute**:

```json
[
  {"id": "bio-001", "text": "Photosynthesis converts sunlight into energy.", "tags": {"category": "biology"}},
  {"id": "bio-002", "text": "DNA replication copies genetic material.", "tags": {"category": "biology"}},
  {"id": "phys-001", "text": "Quantum entanglement links particles instantly.", "tags": {"category": "physics"}},
  {"id": "cs-001", "text": "Machine learning finds patterns in data.", "tags": {"category": "computer-science"}}
]
```

Response:
```json
{
  "total": 4,
  "succeeded": 4,
  "failed": 0,
  "errors": []
}
```

## Step 3 — Test partial failure

Using **POST /documents/batch** in Swagger UI, include a bad document:

```json
[
  {"id": "good-doc", "text": "This document is fine."},
  {"id": "bad-doc", "text": ""},
  {"id": "also-good", "text": "This one is also fine."}
]
```

Response:
```json
{
  "total": 3,
  "succeeded": 2,
  "failed": 1,
  "errors": ["[bad-doc]: Text is required and cannot be empty."]
}
```

The good documents were still indexed.

---

## Exercises

### Exercise 8.1 — Batch + search

After batch indexing, use **POST /search/topk** in Swagger UI to search for `"energy"` and verify that results come from multiple documents.

### Exercise 8.2 — Batch + chat

Using **POST /chat** in Swagger UI, try:

```json
{
  "question": "Summarize the topics covered in the indexed documents"
}
```

The LLM should reference biology, physics, and computer science.

### Exercise 8.3 — Run the tests

```bash
cd module-08
dotnet test
```

You should see **48 tests passed**.

---

## ✅ Final Checkpoint

Congratulations — you've completed the entire workshop! Here's everything you built:

| Module | Feature | Tests |
|--------|---------|-------|
| 0 | Setup — Qdrant connection, Swagger, health check | — |
| 1 | Document indexing with embeddings | 10 |
| 2 | Top-K similarity search | 11 |
| 3 | Tag & property metadata storage | 11 |
| 4 | Filtered search (top-K, threshold, metadata) | 20 |
| 5 | Basic RAG chat | 27 |
| 6 | Advanced chat (custom prompts, filters, threshold) | 30 |
| 7 | Text chunking with sentence-boundary awareness | 48 |
| 8 | Batch document indexing | 48 |

### What to explore next

- **Token-aware chunking** — Replace the character-based chunker with `Microsoft.ML.Tokenizers` for exact token counts
- **Streaming chat** — Use `IChatClient.GetStreamingResponseAsync` for real-time token streaming
- **Named vectors** — Store multiple embedding models in the same collection
- **Hybrid search** — Combine dense (semantic) and sparse (keyword) vectors
- **Web UI** — Build a React/Blazor frontend that calls these endpoints
- **Authentication** — Add API keys or OAuth to protect the endpoints

## 🧹 Clean Up

When you’re done exploring, stop everything:

1. **Stop the local API** — press `Ctrl+C` in the terminal where `dotnet run` is running
2. **Stop Docker containers** — from the `module-08` directory:

```bash
docker compose down
```

> **Tip:** If you want to remove all Qdrant data as well, use `docker compose down -v` to also delete the volumes.

See the [completed/](../completed/) folder for the full reference implementation.
