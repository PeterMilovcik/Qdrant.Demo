# Module 3 — Metadata

> **~10 min** · Builds on [Module 2](../module-02/README.md)

## Learning objective

By the end of this module you will have:

- Understood the difference between **tags** (filterable) and **properties** (informational)
- Stored structured metadata alongside document vectors
- Seen how the `tag.` / `prop.` prefix convention keeps the payload organized

---

## Concepts introduced

### Tags vs Properties

When you index a document you can now attach two kinds of metadata:

| | Tags | Properties |
|---|---|---|
| **Payload prefix** | `tag.{key}` | `prop.{key}` |
| **Purpose** | Filtering during search | Returned with results for display |
| **Example** | `"category": "science"` | `"source_url": "https://..."` |
| **Indexed by Qdrant?** | Yes — used in `must` / `should` filter clauses | No — stored but not used for filtering |

**Why the prefix?** Qdrant stores all metadata in a flat key-value payload. Prefixing tags with `tag.` and properties with `prop.` avoids collisions and makes it easy for the filter factory (coming in Module 4) to build filter clauses automatically.

### Example payload after indexing

```json
{
  "text": "Photosynthesis converts sunlight into chemical energy...",
  "indexed_at_ms": 1718500000000,
  "tag.category": "biology",
  "tag.level": "introductory",
  "prop.source_url": "https://example.com/biology/photosynthesis",
  "prop.author": "Dr. Green"
}
```

---

## What changed from Module 2

| Changed file | What changed |
|-------------|-------------|
| `Models/PayloadKeys.cs` | Added `TagPrefix = "tag."` and `PropertyPrefix = "prop."` constants |
| `Services/DocumentIndexer.cs` | After building the base payload, loops over `request.Tags` and `request.Properties`, writing prefixed keys into the point's payload |

No new endpoints — the existing `POST /documents` already accepts `tags` and `properties` in the request body (they were optional fields since Module 1). Now they are actually **stored**.

---

## Step 1 — Start Qdrant and run the API

```bash
cd module-03
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

## Step 2 — Index a document with metadata

1. Open **Swagger UI** in your browser: **http://localhost:8080/swagger**
2. Find the **POST /documents** endpoint, click **Try it out**
3. Paste the following JSON body and click **Execute**:

```json
{
  "id": "bio-001",
  "text": "Photosynthesis is the process by which green plants convert sunlight into chemical energy.",
  "tags": { "category": "biology", "level": "introductory" },
  "properties": { "source_url": "https://example.com/bio", "author": "Dr. Green" }
}
```

## Step 3 — Verify the metadata was stored

In **Swagger UI**, find the **POST /search/topk** endpoint, click **Try it out**, paste the following body and click **Execute**:

```json
{
  "queryText": "photosynthesis",
  "k": 1
}
```

In the response payload you should now see:

```json
{
  "text": "Photosynthesis is the process...",
  "indexed_at_ms": 1718500000000,
  "tag.category": "biology",
  "tag.level": "introductory",
  "prop.source_url": "https://example.com/bio",
  "prop.author": "Dr. Green"
}
```

The tags and properties are stored alongside the vector — ready for filtering in the next module.

---

## Exercises

### Exercise 3.1 — Index several documents with different tags

Using **POST /documents** in Swagger UI, index these two documents (paste each body and click **Execute**):

**Document 1:**

```json
{
  "id": "phys-001",
  "text": "Quantum entanglement links two particles so the state of one instantly affects the other.",
  "tags": { "category": "physics", "level": "advanced" },
  "properties": { "source_url": "https://example.com/physics" }
}
```

**Document 2:**

```json
{
  "id": "cs-001",
  "text": "Machine learning algorithms learn patterns from data without being explicitly programmed.",
  "tags": { "category": "computer-science", "level": "intermediate" }
}
```

### Exercise 3.2 — Observe tags in search results

Using **POST /search/topk** in Swagger UI, search with:

```json
{
  "queryText": "particles linked together",
  "k": 3
}
```

The physics document should rank highest — and you should see its `tag.category` = `"physics"` in the payload.

### Exercise 3.3 — Think ahead

Right now tags are stored but search still returns all documents ranked by similarity. In the next module you'll add **filters** so you can say "search only biology documents" or "only introductory level" — and Qdrant will narrow the candidate set *before* computing similarity.

### Exercise 3.4 — Run the tests

```bash
cd module-03
dotnet test
```

You should see **11 tests passed** (same count — no new test file this module, but the existing model tests already validate Tags/Properties).

---

## ✅ Checkpoint

At this point you have:

- [x] Tags stored as `tag.{key}` and properties stored as `prop.{key}` in Qdrant
- [x] Documents enriched with structured metadata
- [x] Understanding of: Tags (filterable) vs Properties (informational), prefix convention

## 🧹 Clean Up

Before moving to the next module, stop everything started in this module:

1. **Stop the local API** — press `Ctrl+C` in the terminal where `dotnet run` is running
2. **Stop Docker containers** — from the `module-03` directory:

```bash
docker compose down
```

This stops Qdrant so the next module starts fresh.

**Next →** [Module 4 — Filtered Search](../module-04/README.md)
