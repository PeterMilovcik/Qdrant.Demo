# Module 7 — Chunking

> **~20 min** · Builds on [Module 6](../module-06/README.md)

## Learning objective

By the end of this module you will have:

- Understood **why** long documents need to be split before embedding
- Implemented a character-based **text chunker** with sentence-boundary awareness
- Stored multi-chunk documents with parent-child metadata in Qdrant
- Seen how chunked documents appear in search results

---

## Concepts introduced

### Why chunk?

Embedding models have a **token limit** (e.g. `text-embedding-3-small` has a context window of 8,191 tokens). If your document is longer than that, the API call fails. Even shorter documents can produce lower-quality embeddings when the text is too long — the model has to compress too much meaning into a single vector.

**Chunking** splits a long document into smaller pieces, each of which gets its own embedding vector. This means:
- Each chunk is within the model's token limit
- Each vector represents a focused piece of meaning
- Search can return the most relevant *section*, not just the whole document

### Character-based approach

Our `TextChunker` uses **character counts** instead of token counts. Since typical tokenizers average ~4 characters per English token, 2000 characters ≈ 500 tokens — safely under the 8,191 limit.

### Sentence-boundary awareness

Naive chunking cuts at a fixed character count, which can break mid-sentence:

> `"Photosynthesis is the process by which green pla"` ← bad cut

The `TextChunker` scans backwards from the cut point to find the nearest sentence boundary (`.`, `?`, `!`, or `\n`), producing cleaner chunks.

### Overlap

Adjacent chunks share some text (default: 200 characters) so that context at boundaries isn't lost. If a sentence straddles two chunks, the overlap ensures it appears in at least one of them completely.

### Chunking metadata

When a document is split into multiple chunks, each Qdrant point gets extra payload fields:

| Field | Value |
|-------|-------|
| `source_doc_id` | The original document's point ID |
| `chunk_index` | Zero-based position (0, 1, 2, …) |
| `total_chunks` | How many chunks this document produced |

This lets you group search results by source document or fetch all chunks for a given document.

---

## What changed from Module 6

| New file | Purpose |
|----------|---------|
| `Models/ChunkingOptions.cs` | `MaxChunkSize` (default 2000) and `Overlap` (default 200) configuration |
| `Models/TextChunk.cs` | `TextChunk` record: Text, Index, StartOffset, EndOffset |
| `Services/ITextChunker.cs` | Interface for text chunking |
| `Services/TextChunker.cs` | Character-based chunker with sentence-boundary awareness |
| `Tests/TextChunkerTests.cs` | 18 tests covering short text, multi-chunk, overlap, boundary detection, edge cases |

| Changed file | What changed |
|-------------|-------------|
| `Models/PayloadKeys.cs` | Added `SourceDocId`, `ChunkIndex`, `TotalChunks` constants |
| `Models/Requests.cs` | `DocumentUpsertResponse` now includes `TotalChunks` and `ChunkPointIds` |
| `Services/DocumentIndexer.cs` | Full rewrite: chunks text, creates a point per chunk, stores chunking metadata |
| `Program.cs` | Registers `ChunkingOptions`, `ITextChunker`; passes chunker to `DocumentIndexer` |

### Code walkthrough

#### The text chunker — [`TextChunker.cs`](src/Qdrant.Demo.Api/Services/TextChunker.cs)

If the text fits in a single chunk, it's returned as-is. Otherwise, the chunker splits with overlap and tries to break at sentence boundaries:

```csharp
public IReadOnlyList<TextChunk> Chunk(string text)
{
    if (text.Length <= options.MaxChunkSize)
    {
        return [new TextChunk(text, Index: 0, StartOffset: 0, EndOffset: text.Length)];
    }

    List<TextChunk> chunks = [];
    var chunkIndex = 0;
    var start = 0;

    while (start < text.Length)
    {
        var remaining = text.Length - start;
        var length = Math.Min(options.MaxChunkSize, remaining);

        // Not the last chunk? Try to find a sentence boundary to break at.
        if (start + length < text.Length)
        {
            length = FindSentenceBoundary(text, start, length);
        }
        // ... store chunk and advance by (length - overlap)
    }
}
```

The `FindSentenceBoundary` method scans backwards from the cut point, preferring paragraph breaks (`\n`), then sentence enders (`.`, `?`, `!`), then any whitespace — and falls back to a hard cut only as a last resort.

#### Chunked indexing — [`DocumentIndexer.cs`](src/Qdrant.Demo.Api/Services/DocumentIndexer.cs)

The indexer now creates one Qdrant point per chunk. Single-chunk documents keep the original id; multi-chunk documents get a derived id per chunk:

```csharp
// For single-chunk documents keep the original id;
// for multi-chunk, append _chunk_{index} for uniqueness.
var pointIdStr = chunks.Count == 1
    ? sourceId
    : $"{sourceId}_chunk_{i}".ToDeterministicGuid().ToString("D");
```

Each chunk carries parent-child metadata so search results can be grouped by source document:

```csharp
if (chunks.Count > 1)
{
    point.Payload[SourceDocId] = sourceId;
    point.Payload[ChunkIndex] = i.ToString();
    point.Payload[TotalChunks] = chunks.Count.ToString();
}
```

Tags and properties from the original request are copied to **every** chunk, so tag-filtered searches still match regardless of which chunk is most relevant.

---

## Step 1 — Start Qdrant and run the API

```bash
cd module-07
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

Visit `http://localhost:8080/` — the response now includes a `chunking` section showing the configured `maxChunkSize` and `overlap`.

## Step 2 — Index a short document (single chunk)

```bash
curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{"id": "short-001", "text": "Photosynthesis converts sunlight into chemical energy."}'
```

Response:
```json
{
  "pointId": "abc-123...",
  "totalChunks": 1,
  "chunkPointIds": ["abc-123..."]
}
```

`totalChunks: 1` — the text was short enough for a single embedding.

## Step 3 — Index a long document (multiple chunks)

Create a document longer than 2000 characters:

```bash
curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d "{\"id\": \"long-001\", \"text\": \"$(python3 -c 'print("This is a long sentence about biology. " * 100)')\"}"
```

```powershell
# PowerShell alternative
$text = ("This is a long sentence about biology. " * 100)
curl -Method Post http://localhost:8080/documents \
  -ContentType "application/json" \
  -Body ("{\"id\":\"long-001\",\"text\":\"$text\"}")
```

Or use any text file content. The response should show `totalChunks > 1`.

## Step 4 — Search and observe chunk results

```bash
curl -X POST http://localhost:8080/search/topk \
  -H "Content-Type: application/json" \
  -d '{"queryText": "biology", "k": 5}'
```

You may see multiple chunks from the same source document in the results, each with its own score. The payload will include `source_doc_id`, `chunk_index`, and `total_chunks`.

---

## Exercises

### Exercise 7.1 — Custom chunk size

Set a small chunk size via environment variable and re-run:

```bash
CHUNKING_MAX_SIZE=200 CHUNKING_OVERLAP=50 dotnet run
```

```powershell
# PowerShell alternative
$env:CHUNKING_MAX_SIZE = "200"
$env:CHUNKING_OVERLAP = "50"
dotnet run
```

Index the same long document and observe that it produces many more chunks.

### Exercise 7.2 — Inspect sentence boundaries

Index this text and check where the chunks split:

```
First paragraph is about biology. Photosynthesis is important. Plants need sunlight.
Second paragraph discusses physics. Quantum mechanics is complex. Particles behave strangely.
Third paragraph covers chemistry. Chemical reactions produce energy. Molecules combine.
```

The chunker should prefer splitting at paragraph breaks or sentence ends.

### Exercise 7.3 — Run the tests

```bash
cd module-07
dotnet test
```

You should see **48 tests passed** (30 prior + 18 TextChunker).

---

## ✅ Checkpoint

At this point you have:

- [x] Automatic text chunking with configurable size and overlap
- [x] Sentence-boundary-aware splitting (no mid-sentence cuts)
- [x] Multi-chunk documents with parent-child metadata in Qdrant
- [x] Understanding of: chunking strategy, overlap, sentence boundaries, chunking metadata

## 🧹 Clean Up

Before moving to the next module, stop everything started in this module:

1. **Stop the local API** — press `Ctrl+C` in the terminal where `dotnet run` is running
2. **Stop Docker containers** — from the `module-07` directory:

```bash
docker compose down
```

This stops Qdrant so the next module starts fresh.

**Next →** [Module 8 — Batch Operations](../module-08/README.md)
