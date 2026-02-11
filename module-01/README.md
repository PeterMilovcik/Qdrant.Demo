# Module 1 — Your First Document

> **~15 min** · Requires Ollama (started via Docker Compose) · Builds on [Module 0](../module-00/README.md)

## Learning objective

By the end of this module you will have:

- Understood what **embeddings** are and why they matter
- Indexed your first document into Qdrant via `POST /documents`
- Seen a real vector in the Qdrant Dashboard
- Understood **deterministic point-ids** and **idempotent upserts**

---

## Concepts introduced

### What are embeddings?

An **embedding** is a list of floating-point numbers (a **vector**) that captures the *meaning* of a piece of text. The Ollama `nomic-embed-text` model produces a vector of **768 floats** for any input text.

**Key insight:** Texts with similar meaning produce vectors that are close together in vector space. "The cat sat on the mat" and "A kitten was sitting on a rug" will have very similar vectors, even though the words are different.

This is what makes semantic search possible — instead of matching keywords, we compare the *meaning* of texts by comparing their vectors.

### How a document becomes a Qdrant point

When you call `POST /documents`, the API:

1. **Hashes the id** (or the text, if no id is given) into a deterministic UUID — the `pointId`
2. **Calls Ollama** to generate an embedding (768 floats) from the text
3. **Builds a Qdrant point** with the UUID, the vector, and a payload containing the text + a timestamp
4. **Upserts** the point into Qdrant (insert-or-update — if the point-id already exists, it's overwritten)

### Deterministic point-ids

The API generates point-ids using SHA-256 hashing:

- If you provide an `id` field → `pointId = SHA256("article-001")` → same UUID every time
- If you omit `id` → `pointId = SHA256(text)` → same text always maps to the same point

This means **re-indexing the same document is safe** — it just overwrites the existing point. No duplicates, ever. This is called an **idempotent upsert**.

---

## What changed from Module 0

| New file | Purpose |
|----------|---------|
| `Extensions/StringExtensions.cs` | `ToDeterministicGuid()` — hashes a string to a UUID |
| `Models/PayloadKeys.cs` | Constants for payload field names (`text`, `indexed_at_ms`) |
| `Models/Requests.cs` | `DocumentUpsertRequest` and `DocumentUpsertResponse` DTOs |
| `Services/IEmbeddingService.cs` | Interface for text → vector conversion |
| `Services/EmbeddingService.cs` | Ollama implementation of the embedding service (via `IEmbeddingGenerator<string, Embedding<float>>`) |
| `Services/IDocumentIndexer.cs` | Interface for the embed + upsert pipeline |
| `Services/DocumentIndexer.cs` | Implementation: hash id → embed → build point → upsert |
| `Endpoints/DocumentEndpoints.cs` | `POST /documents` endpoint |

| Changed file | What changed |
|-------------|-------------|
| `Program.cs` | Added Ollama config, `IEmbeddingGenerator<string, Embedding<float>>`, `IEmbeddingService`, `IDocumentIndexer`, `MapDocumentEndpoints()` |
| `Qdrant.Demo.Api.csproj` | Added `OllamaSharp` and `Microsoft.Extensions.AI` NuGet packages |
| `appsettings.json` | Added `Ollama.EmbeddingModel` |
| `docker-compose.yml` | Added `demo-api` service with Ollama env vars; Ollama runs as a Docker service with automatic model pulling (via `ollama-pull` init container) |

---

## Step 1 — Note on Ollama

Ollama runs as a Docker service — no API key needed. When you run `docker compose up` in the next step, the `ollama-pull` init container will automatically download the required models (`nomic-embed-text` for embeddings). The first run may take a few minutes (~274 MB download).

## Step 2 — Start Qdrant and run the API

```bash
cd module-01
docker compose up -d    # starts Qdrant + demo-api (http://localhost:8080)

# Option A: use the containerized API at http://localhost:8080
# Option B: run the API locally on a known port
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

## Step 3 — Index your first document

Open Swagger UI or use curl:

```bash
curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "article-001",
    "text": "Photosynthesis is the process by which green plants convert sunlight into chemical energy, producing oxygen as a byproduct."
  }'
```

Response:

```json
{
  "pointId": "a1b2c3d4-..."
}
```

## Step 4 — Verify in Qdrant Dashboard

Open **http://localhost:6333/dashboard** → click the `documents` collection.

You should see **1 point**. Click on it to inspect:
- **Id:** the deterministic UUID
- **Vector:** 768 floating-point numbers (the embedding!)
- **Payload:** `text` (your document text) and `indexed_at_ms` (timestamp)

This is what an embedding looks like in practice — a long list of numbers that captures the meaning of your text.

## Step 5 — Index two more documents

```bash
curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "article-002",
    "text": "Quantum entanglement is a phenomenon where two particles become linked, so the quantum state of one instantly influences the other, regardless of distance."
  }'
```

```bash
curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "article-003",
    "text": "Machine learning is a subset of artificial intelligence where algorithms learn patterns from data rather than being explicitly programmed."
  }'
```

Check the Dashboard — you should now see **3 points**.

---

## Exercises

### Exercise 1.1 — Re-index and observe idempotency

Index `article-001` again with the **exact same id**:

```bash
curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "article-001",
    "text": "Photosynthesis is the process by which green plants convert sunlight into chemical energy, producing oxygen as a byproduct."
  }'
```

Check the Dashboard — you should still see **3 points**, not 4. The existing point was overwritten. The `pointId` in the response is identical to the first time.

### Exercise 1.2 — Index without an explicit id

```bash
curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "text": "Water boils at 100 degrees Celsius at sea level."
  }'
```

Notice: the `pointId` is derived from the text hash. If you index the exact same text again, you'll get the same `pointId` — still idempotent.

### Exercise 1.3 — Run the tests

```bash
cd module-01
dotnet test
```

You should see **10 tests passed** — covering `StringExtensions` (4 tests) and `DocumentIndexer` model shapes (6 tests).

---

## ✅ Checkpoint

At this point you have:

- [x] 3+ documents indexed in Qdrant
- [x] Seen real embeddings (768-dimensional vectors) in the Dashboard
- [x] Verified idempotent upserts (re-indexing doesn't create duplicates)
- [x] Understanding of: embeddings, points, payloads, deterministic point-ids

**Next →** [Module 2 — Similarity Search](../module-02/README.md)
