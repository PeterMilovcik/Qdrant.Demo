# Module 2 — Similarity Search

> **~15 min** · Builds on [Module 1](../module-01/README.md)

## Learning objective

By the end of this module you will have:

- Understood **cosine similarity** and what search scores mean
- Used `POST /search/topk` to find documents similar to a query
- Observed how results are ranked by semantic relevance, not keyword matching

---

## Concepts introduced

### Cosine similarity

When you search, the API embeds your query text into a vector (just like indexing), then compares it against every stored vector using **cosine similarity**.

Cosine similarity measures the angle between two vectors:
- **1.0** = identical meaning (vectors point in the same direction)
- **0.0** = completely unrelated
- Values typically range **0.3 – 0.9** for real-world text

**Key insight:** This is *semantic* search, not keyword search. The query "How do plants make food?" will match a document about "photosynthesis" even though the words don't overlap — because their *meanings* are close in vector space.

### Top-K search

`POST /search/topk` returns exactly **K** results, ranked by cosine similarity (highest first). If K=3, you get the 3 most similar documents, regardless of how similar they actually are.

Use Top-K when you want a **fixed number of results** — like "show me the 5 most relevant documents."

---

## What changed from Module 1

| New file | Purpose |
|----------|---------|
| `Extensions/QdrantPayloadExtensions.cs` | Converts gRPC payload to clean JSON for API responses |
| `Endpoints/SearchEndpoints.cs` | `POST /search/topk` — embed query → search Qdrant → return ranked hits |

| Changed file | What changed |
|-------------|-------------|
| `Models/Requests.cs` | Added `TopKSearchRequest` and `SearchHit` records |
| `Program.cs` | Added `MapSearchEndpoints(collectionName)` |

---

## Step 1 — Start Qdrant and run the API

```bash
cd module-02
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

## Step 2 — Index some documents

If you haven't already, index the sample articles:

```bash
curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{"id": "article-001", "text": "Photosynthesis is the process by which green plants convert sunlight into chemical energy, producing oxygen as a byproduct."}'

curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{"id": "article-002", "text": "Quantum entanglement is a phenomenon where two particles become linked, so the quantum state of one instantly influences the other, regardless of distance."}'

curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{"id": "article-003", "text": "Machine learning is a subset of artificial intelligence where algorithms learn patterns from data rather than being explicitly programmed."}'
```

## Step 3 — Search!

```bash
curl -X POST http://localhost:8080/search/topk \
  -H "Content-Type: application/json" \
  -d '{"queryText": "How do plants produce energy from sunlight?", "k": 3}'
```

Response — all 3 documents ranked by similarity:

```json
[
  {
    "id": "a1b2c3d4-...",
    "score": 0.89,
    "payload": {
      "text": "Photosynthesis is the process by which green plants...",
      "indexed_at_ms": 1234567890000
    }
  },
  {
    "id": "...",
    "score": 0.42,
    "payload": { "text": "Quantum entanglement is..." }
  },
  {
    "id": "...",
    "score": 0.38,
    "payload": { "text": "Machine learning is..." }
  }
]
```

**Notice:** The photosynthesis article scores highest (~0.89) because it's semantically closest to the query. The other articles score much lower because they're about different topics. This is cosine similarity in action.

---

## Exercises

### Exercise 2.1 — Try different queries

Search for each of these and observe which document scores highest:

```bash
# Should match article-002 (quantum physics)
curl -X POST http://localhost:8080/search/topk \
  -H "Content-Type: application/json" \
  -d '{"queryText": "spooky action at a distance", "k": 3}'

# Should match article-003 (ML/AI)
curl -X POST http://localhost:8080/search/topk \
  -H "Content-Type: application/json" \
  -d '{"queryText": "training algorithms on datasets", "k": 3}'
```

### Exercise 2.2 — Change K

Try `"k": 1` to get only the single best match. Then try `"k": 10` — with only 3 documents indexed, you'll still get at most 3 results.

### Exercise 2.3 — Search for something unrelated

```bash
curl -X POST http://localhost:8080/search/topk \
  -H "Content-Type: application/json" \
  -d '{"queryText": "best pizza recipe", "k": 3}'
```

You'll still get 3 results (Top-K always returns K results if the collection has enough), but the scores will be noticeably lower. There's no "minimum relevance" filter yet — that comes in Module 4 (threshold search).

### Exercise 2.4 — Run the tests

```bash
cd module-02
dotnet test
```

You should see **11 tests passed**.

---

## ✅ Checkpoint

At this point you have:

- [x] A working `POST /search/topk` endpoint
- [x] Observed semantic search in action — meaning-based, not keyword-based
- [x] Understood cosine similarity scores
- [x] Understanding of: Top-K search, score interpretation, QdrantPayloadExtensions

**Next →** [Module 3 — Metadata](../module-03/README.md)
