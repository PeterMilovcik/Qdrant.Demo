# Qdrant.Demo — RAG Workshop Template

A hands-on **.NET 10 + Qdrant + OpenAI** workshop template that teaches how to build a complete **Retrieval-Augmented Generation (RAG)** solution from scratch. Index any text with metadata, search it three different ways, and **chat with your documents** using OpenAI.

---

## What is RAG?

**Retrieval-Augmented Generation** is a pattern where you:

1. **Index** — turn your documents into vectors (embeddings) and store them in a vector database.
2. **Retrieve** — given a user query, embed it and find the most similar documents.
3. **Generate** — feed the retrieved documents into an LLM to produce a grounded answer.

This workshop implements **all three steps**. You'll learn how embeddings work, how Qdrant stores and searches vectors, how to combine vector similarity with metadata filters, and how to generate LLM-powered answers grounded in your own data.

```
┌──────────────┐      embed       ┌───────────────┐
│  Your Text   │  ──────────────► │  Qdrant       │
│  + metadata  │                  │  (vectors +   │
└──────────────┘                  │   payload)    │
                                  └───────┬───────┘
┌──────────────┐      embed + search      │
│  User Query  │  ──────────────────────► │
└──────────────┘                          ▼
                                  ┌───────────────┐
                                  │  Ranked       │
                                  │  Results      │──────┐
                                  └───────────────┘      │  context
                                                         ▼
                                                  ┌──────────────┐
                                                  │  OpenAI LLM  │
                                                  │  (chat)      │
                                                  └──────┬───────┘
                                                         ▼
                                                  ┌──────────────┐
                                                  │  Grounded    │
                                                  │  Answer      │
                                                  └──────────────┘
```

---

## What you'll learn

By the end of this workshop you will understand:

1. **What embeddings are** — how text is converted into numerical vectors that capture meaning.
2. **How vector similarity search works** — finding the closest vectors using cosine similarity.
3. **How Qdrant stores and queries data** — collections, points, payloads, and filters.
4. **The difference between filterable and informational metadata** — tags vs properties.
5. **How to ground LLM answers in your own data** — the RAG pattern that prevents hallucination.
6. **How to handle documents that exceed the embedding model's token limit** — chunking strategies.
7. **How to build a production-shaped .NET API** — minimal APIs, dependency injection, service abstractions.

---

## Glossary

| Term | Definition |
|------|------------|
| **Embedding** | A fixed-length array of floating-point numbers (a _vector_) that represents the semantic meaning of text. Similar texts produce similar vectors. |
| **Vector** | A list of numbers (e.g. 1 536 floats). In this context, it is the embedding of a piece of text. |
| **Cosine similarity** | A measure of how similar two vectors are, ranging from 0 (unrelated) to 1 (identical meaning). |
| **Collection** | A Qdrant container that holds vectors of the same dimensionality — analogous to a database table. |
| **Point** | A single entry in a Qdrant collection: a unique id + a vector + an optional payload (metadata). |
| **Payload** | Arbitrary key/value metadata stored alongside a vector in Qdrant. Can be indexed for filtering. |
| **Upsert** | Insert-or-update: if the point-id already exists the point is overwritten, otherwise it is created. |
| **Grounding** | Providing an LLM with factual context (retrieved documents) so it answers based on evidence, not imagination. |
| **Hallucination** | When an LLM generates plausible-sounding but factually incorrect information. RAG reduces this. |
| **System prompt** | An instruction message sent to the LLM before the user's question, controlling its behaviour and constraints. |
| **Chunk** | A smaller segment of a long document. Documents that exceed the embedding model's token limit are split into overlapping chunks, each embedded and stored as a separate Qdrant point. |
| **Overlap** | Characters shared between consecutive chunks so that context is not lost at chunk boundaries. |

| Component | Role |
|-----------|------|
| **Qdrant** (Docker) | Open-source vector database — stores embeddings + payload metadata |
| **.NET 10 Web API** | Minimal API that exposes indexing, search, and chat endpoints |
| **OpenAI Embeddings** | `text-embedding-3-small` model (1 536 dimensions) converts text → vectors |
| **OpenAI Chat** | `gpt-4.1-nano` model (configurable) generates answers grounded in retrieved documents |
| **Docker Compose** | Runs both services in a private network |

---

## Prerequisites

| Tool | Version | Why |
|------|---------|-----|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | 4.x+ | Runs Qdrant + API containers |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | 10.0+ | Build/debug locally (optional — Docker handles it too) |
| [OpenAI API key](https://platform.openai.com/api-keys) | — | Used for `text-embedding-3-small` embeddings |
| `curl` or similar HTTP client | — | To test the API endpoints |

> **💰 Cost note:** A full workshop run with ~10 documents costs **< $0.01** in OpenAI API usage (embedding + chat). You will not incur meaningful charges.

---

## Folder layout

```
Qdrant.Demo/
  .gitignore
  docker-compose.yml
  Qdrant.Demo.sln
  README.md
  src/
    Qdrant.Demo.Api/
      .dockerignore
      Dockerfile
      Qdrant.Demo.Api.csproj
      Program.cs
      appsettings.json
      Endpoints/
        ChatEndpoints.cs         # POST /chat (retrieve + generate)
        DocumentEndpoints.cs     # POST /documents + /documents/batch
        SearchEndpoints.cs       # POST /search/topk, /search/threshold, /search/metadata
      Extensions/
        StringExtensions.cs      # ToDeterministicGuid()
        DateTimeExtensions.cs    # ToUnixMs()
        QdrantPayloadExtensions.cs  # ToDictionary(), ToFormattedHits()
      Models/
        ChunkingOptions.cs       # chunking configuration (MaxChunkSize, Overlap)
        PayloadKeys.cs           # shared Qdrant payload field-name constants
        Requests.cs              # all DTOs — upsert, search, chat, and SearchHit
        TextChunk.cs             # chunk data record (Text, Index, offsets)
      Services/
        IEmbeddingService.cs     # text → vector abstraction
        EmbeddingService.cs      # OpenAI implementation
        IQdrantFilterFactory.cs  # tag filter builder abstraction
        QdrantFilterFactory.cs   # gRPC + REST filter implementations
        IDocumentIndexer.cs      # embed + upsert abstraction
        DocumentIndexer.cs       # production implementation (with chunking)
        ITextChunker.cs          # text → chunks abstraction
        TextChunker.cs           # character-based chunker with sentence-boundary awareness
        QdrantBootstrapper.cs    # BackgroundService — collection bootstrap
  tests/
    Qdrant.Demo.Api.Tests/
      Qdrant.Demo.Api.Tests.csproj
      StringExtensionsTests.cs
      DateTimeExtensionsTests.cs
      DocumentIndexerTests.cs
      QdrantFilterFactoryTests.cs
      TextChunkerTests.cs
      ChatTests.cs
```

---

## Key concept: Tags vs Properties

When you index a document, you provide metadata in two explicit categories:

### Tags — indexed, filterable

```json
"tags": { "category": "science", "author": "Jane", "language": "en" }
```

**Tags** are stored in the Qdrant payload with a `tag.` prefix (e.g. `tag.category`) and are **indexed** as Qdrant `keyword` fields. You can use them in **search filters** to narrow results — for example, *"find similar documents where category = science."*

**Use tags for:** any attribute you might want to **filter by** when searching:

| Example tag key | Use case |
|----------------|----------|
| `category` | Filter by topic (science, history, engineering…) |
| `author` | Find documents by a specific author |
| `language` | Restrict results to a language (`en`, `sk`, `de`…) |
| `source` | Where the document came from (wikipedia, internal-wiki, arxiv…) |
| `department` | Organisational unit that owns the content |
| `document_type` | Article, FAQ, tutorial, report… |
| `status` | Published, draft, archived… |
| `priority` | High, medium, low |

### Properties — informational, not filterable

```json
"properties": { "source_url": "https://...", "page": "42", "note": "intro chapter" }
```

**Properties** are stored in the Qdrant payload with a `prop.` prefix (e.g. `prop.source_url`). They are **not indexed** — they are returned alongside search results so the consumer has context, but they **cannot be used in filters**.

**Use properties for:** any supplementary information a consumer needs when reading results:

| Example property key | Use case |
|---------------------|----------|
| `source_url` | Link back to the original document |
| `page` | Page number within a larger document |
| `summary` | Short summary or excerpt |
| `created_date` | When the document was written |
| `last_updated` | When the document was last modified |
| `note` | Free-form annotation |
| `image_url` | Associated image or thumbnail |
| `word_count` | Length of the original document |

### Why the distinction?

| Aspect | Tags | Properties |
|--------|------|------------|
| Stored in Qdrant payload | ✅ as `tag.{key}` | ✅ as `prop.{key}` |
| Indexed (payload index) | ✅ keyword index | ❌ no index |
| Usable in search filters | ✅ | ❌ |
| Returned with search results | ✅ | ✅ |
| Performance impact | Indexes use memory | None |

> **Workshop tip:** To make tag filtering fast, create a Qdrant payload index for each tag field via the **Qdrant Dashboard** at `http://localhost:6333/dashboard` → select your collection → "Payload Index" → add a `keyword` index on `tag.category` (or whichever fields you use). This is a great hands-on exercise!

---

## docker-compose.yml

Qdrant's REST port (6333) is published to the host so you can browse the **Qdrant Dashboard** at `http://localhost:6333/dashboard`. gRPC (6334) stays internal — only the API container uses it.

> ⚠️ **Local dev only.** Port 6333 is published without an API key. In production, either keep Qdrant on an internal network or configure a [Qdrant API key](https://qdrant.tech/documentation/guides/security/).

```yaml
services:
  qdrant:
    image: qdrant/qdrant:v1.16.3
    volumes:
      - qdrant_storage:/qdrant/storage
    expose:
      - "6334"
    ports:
      - "6333:6333"
    networks:
      - backend

  demo-api:
    build:
      context: ./src/Qdrant.Demo.Api
      dockerfile: Dockerfile
    environment:
      ASPNETCORE_URLS: http://+:8080
      QDRANT_HOST: qdrant
      QDRANT_HTTP_PORT: "6333"
      QDRANT_GRPC_PORT: "6334"
      OPENAI_API_KEY: ${OPENAI_API_KEY}
      QDRANT_COLLECTION: documents
      EMBEDDING_DIM: "1536"
      OPENAI_EMBEDDING_MODEL: text-embedding-3-small
      OPENAI_CHAT_MODEL: gpt-4.1-nano
    depends_on:
      - qdrant
    ports:
      - "8080:8080"
    networks:
      - backend

volumes:
  qdrant_storage:

networks:
  backend:
    driver: bridge
```

---

## Application code

The code follows **.NET minimal API best practices** — thin entry point, extension methods for endpoint registration, separated services and models:

| File | Responsibility |
|------|---------------|
| `Program.cs` | Config, DI, endpoint registration, Swagger |
| `Endpoints/ChatEndpoints.cs` | `POST /chat` — retrieve + generate (full RAG) |
| `Endpoints/DocumentEndpoints.cs` | `POST /documents` + `POST /documents/batch` |
| `Endpoints/SearchEndpoints.cs` | `POST /search/topk` + `/search/threshold` + `/search/metadata` |
| `Extensions/StringExtensions.cs` | `ToDeterministicGuid()` extension on `string` |
| `Extensions/DateTimeExtensions.cs` | `ToUnixMs()` extension on `DateTime` |
| `Extensions/QdrantPayloadExtensions.cs` | `ToDictionary()` + `ToFormattedHits()` on Qdrant types |
| `Models/PayloadKeys.cs` | Shared constants for Qdrant payload field names |
| `Models/Requests.cs` | All DTOs — upsert, search, chat, and `SearchHit` |
| `Services/IEmbeddingService.cs` + `EmbeddingService.cs` | Text → vector embedding abstraction |
| `Services/IQdrantFilterFactory.cs` + `QdrantFilterFactory.cs` | Tag → Qdrant filter (gRPC + REST) |
| `Services/IDocumentIndexer.cs` + `DocumentIndexer.cs` | Embed + upsert abstraction |
| `Services/QdrantBootstrapper.cs` | `BackgroundService` — creates collection at startup with retries |

> **⚠️ Qdrant.Client SDK gotchas:**
>
> 1. **`Range` type ambiguity**: `Range` in Qdrant.Client collides with `System.Range`. Always fully qualify as `Qdrant.Client.Grpc.Range`.
> 2. **`SearchAsync` parameters**: Use `payloadSelector: true` (not `withPayload`). The `limit` parameter is `ulong` (not `uint`). Use `scoreThreshold: float?` to cut off low-similarity noise. The `filter` parameter expects `Filter?`, not `Condition?` — wrap conditions: `new Filter { Must = { condition } }`.
> 3. **Payload `Value` type**: Qdrant.Client uses `Qdrant.Client.Grpc.Value` (not `Google.Protobuf.WellKnownTypes.Value`). Use `KindOneofCase.DoubleValue` and `IntegerValue` (there is no `NumberValue`).

---

## Run it

```bash
# Export your OpenAI API key
# Linux/macOS:
export OPENAI_API_KEY="sk-..."

# Windows PowerShell:
$env:OPENAI_API_KEY = "sk-..."

# Or create a .env file (recommended — Docker Compose reads it automatically):
# echo OPENAI_API_KEY=sk-... > .env

# Start everything
docker compose up --build
```

The API is available at **http://localhost:8080/**

Swagger UI is available at **http://localhost:8080/swagger** — use it to explore and test all endpoints interactively.

You should see bootstrap logs like:

```
demo-api-1  | [bootstrap] Collection 'documents' ready.
demo-api-1  | info: Microsoft.Hosting.Lifetime[14]
demo-api-1  |       Now listening on: http://[::]:8080
```

---

## API Reference

### `POST /documents` — Index a single document

```bash
curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "article-001",
    "text": "Photosynthesis is the process by which green plants convert sunlight into chemical energy, producing oxygen as a byproduct.",
    "tags": {
      "category": "biology",
      "level": "introductory"
    },
    "properties": {
      "source_url": "https://en.wikipedia.org/wiki/Photosynthesis",
      "page": "1"
    }
  }'
```

Response:

```json
{ "pointId": "a1b2c3d4-..." }
```

Re-indexing the same `id` returns the **same `pointId`** (idempotent upsert — the point is overwritten, not duplicated).

### `POST /documents/batch` — Index multiple documents at once

```bash
curl -X POST http://localhost:8080/documents/batch \
  -H "Content-Type: application/json" \
  -d '[
    {
      "id": "article-001",
      "text": "Photosynthesis is the process by which green plants convert sunlight into chemical energy.",
      "tags": { "category": "biology" },
      "properties": { "source_url": "https://example.com/photosynthesis" }
    },
    {
      "id": "article-002",
      "text": "Quantum entanglement is a phenomenon where two particles become correlated regardless of distance.",
      "tags": { "category": "physics" },
      "properties": { "source_url": "https://example.com/entanglement" }
    },
    {
      "id": "article-003",
      "text": "Machine learning algorithms improve their performance on a task through experience without being explicitly programmed.",
      "tags": { "category": "computer-science" },
      "properties": { "source_url": "https://example.com/ml" }
    }
  ]'
```

Response:

```json
{
  "total": 3,
  "succeeded": 3,
  "failed": 0,
  "errors": []
}
```

### `POST /search/topk` — Top-K similarity search

Returns exactly **K** results ranked by cosine similarity. Use when you want a fixed number of "best matches."

```bash
curl -X POST http://localhost:8080/search/topk \
  -H "Content-Type: application/json" \
  -d '{
    "queryText": "How do plants produce energy from sunlight?",
    "k": 3,
    "tags": { "category": "biology" }
  }'
```

Response — an array ranked by similarity score:

```json
[
  {
    "id": "a1b2c3d4-...",
    "score": 0.82,
    "payload": {
      "text": "Photosynthesis is the process...",
      "tag.category": "biology",
      "tag.level": "introductory",
      "prop.source_url": "https://en.wikipedia.org/wiki/Photosynthesis",
      "prop.page": "1",
      "indexed_at_ms": 1739107200000
    }
  }
]
```

### `POST /search/threshold` — Threshold similarity search

Returns **all** documents whose cosine similarity score is ≥ the threshold. Use when you want every "good enough" match rather than a fixed count.

```bash
curl -X POST http://localhost:8080/search/threshold \
  -H "Content-Type: application/json" \
  -d '{
    "queryText": "How do plants produce energy from sunlight?",
    "scoreThreshold": 0.4,
    "limit": 100
  }'
```

The response format is the same as `/search/topk`.

### `POST /search/metadata` — Metadata-only search (no vector)

Browse/export documents matching **only** tag filters — no similarity involved.

```bash
curl -X POST http://localhost:8080/search/metadata \
  -H "Content-Type: application/json" \
  -d '{
    "limit": 10,
    "tags": { "category": "biology" }
  }'
```

Response — Qdrant scroll result with points and payloads:

```json
{
  "result": {
    "points": [
      {
        "id": "a1b2c3d4-...",
        "payload": { "text": "...", "tag.category": "biology", "..." }
      }
    ],
    "next_page_offset": null
  },
  "status": "ok",
  "time": 0.001
}
```

### `POST /chat` — Ask a question (full RAG)

The complete RAG pipeline: embed the question → retrieve similar documents from Qdrant → feed them as context to the OpenAI chat model → return a grounded answer with sources.

```bash
curl -X POST http://localhost:8080/chat \
  -H "Content-Type: application/json" \
  -d '{
    "question": "How do plants produce energy from sunlight?",
    "k": 3,
    "tags": { "category": "biology" }
  }'
```

Response:

```json
{
  "answer": "Plants produce energy from sunlight through a process called photosynthesis. During photosynthesis, green plants convert sunlight into chemical energy, producing oxygen as a byproduct.",
  "sources": [
    {
      "id": "a1b2c3d4-...",
      "score": 0.82,
      "textSnippet": "Photosynthesis is the process by which green plants convert sunlight into chemical energy, producing oxygen as a byproduct."
    }
  ]
}
```

Optional fields:

| Field | Default | Description |
|-------|---------|-------------|
| `question` | *(required)* | Natural-language question |
| `k` | `5` | Number of documents to retrieve as context |
| `scoreThreshold` | `null` | Minimum similarity — documents below this are excluded |
| `tags` | `null` | Tag filter for retrieval (same as search endpoints) |
| `systemPrompt` | `null` | Custom system prompt (default: "Answer based only on the provided context…") |
#### How `/chat` works internally

When you call `POST /chat`, the API executes the full RAG pipeline in four steps:

1. **Embed the question** — Your question text is sent to the OpenAI Embeddings API (`text-embedding-3-small`) to produce a 1 536-dimensional vector. This is the same process used when indexing documents.
2. **Search Qdrant** — The question vector is compared against all stored document vectors using cosine similarity. The top-K most similar documents are retrieved (optionally filtered by tags and/or a minimum score threshold).
3. **Build context** — The `text` field from each retrieved document is concatenated into a numbered list. This becomes the "context" the LLM will read.
4. **Generate answer** — A system prompt + user message (context + question) are sent to the OpenAI Chat Completion API. The model reads the context and produces an answer grounded in your data.

The response includes both the generated `answer` and the `sources` array so you can verify which documents contributed.

#### System prompt & hallucination guardrail

> ⚠️ **This is the most important concept in RAG.**

The default system prompt instructs the model:

> *"Answer the user’s question based **only** on the provided context documents. If the context does not contain enough information to answer, say so clearly — do not make up facts."*

This prompt is the **hallucination guardrail**. Without it, the LLM would answer from its general training data — which may be incorrect, outdated, or irrelevant to your domain. With it, the model is constrained to only use the documents you retrieved. If your documents don't contain the answer, the model will say "I don't have enough information" instead of guessing.

You can override this via the `systemPrompt` field to change the model's personality, language, or constraints — but always keep the "only answer from context" instruction unless you intentionally want open-ended generation.

#### Error responses

All endpoints return [RFC 7807 Problem Details](https://datatracker.ietf.org/doc/html/rfc7807) on error:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Chat failed",
  "status": 500,
  "detail": "The error message from the underlying service."
}
```

Input validation errors (e.g. empty `question` or `text`) return **400 Bad Request** with a plain-text message.
---

## Workshop Exercises

### Exercise 1: Index sample data

Index the three articles from the batch example above. Verify they appear in the **Qdrant Dashboard** at `http://localhost:6333/dashboard`.

### Exercise 2: Top-K search

Search for `"energy conversion in cells"` with `k=2`. Which articles come back? What are their scores?

### Exercise 3: Threshold search

Search for `"machine learning"` with `scoreThreshold=0.3`. Try different thresholds (`0.2`, `0.4`, `0.6`) and observe how the result count changes. What threshold feels "right" for your data?

### Exercise 4: Metadata filtering

Add a few more articles with different `category` tags, then use `/search/metadata` to retrieve only `"category": "physics"` documents.

### Exercise 5: Create a payload index (Qdrant Dashboard)

1. Open the Qdrant Dashboard → select the `documents` collection.
2. Navigate to **Payload Index**.
3. Create a `keyword` index on `tag.category`.
4. Observe how filtered searches become faster (especially noticeable with larger datasets).

### Exercise 6: Combine vector search + filters

Use `/search/topk` with both a `queryText` and a `tags` filter. Compare the results with and without the filter — how does narrowing by tag affect what comes back?

### Exercise 7: Idempotent upserts

Index the same document twice with the same `id`. Use the Qdrant Dashboard to verify there's still only one point — not two.

### Exercise 8: Chat with your documents

After indexing the sample articles, ask a question using `POST /chat`:

```bash
curl -X POST http://localhost:8080/chat \
  -H "Content-Type: application/json" \
  -d '{ "question": "How do plants produce energy?" }'
```

Observe how the answer references the indexed content. Try asking something **not** in your documents — does the model refuse to guess?

### Exercise 9: Custom system prompt

Try changing the behaviour of the chat by providing a custom `systemPrompt`:

```bash
curl -X POST http://localhost:8080/chat \
  -H "Content-Type: application/json" \
  -d '{
    "question": "Explain quantum entanglement",
    "systemPrompt": "You are a teacher explaining concepts to a 10-year-old. Use simple language and analogies."
  }'
```

### Exercise 10: Filtered chat

Use `tags` to restrict which documents are used as context:

```bash
curl -X POST http://localhost:8080/chat \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What did I index about science?",
    "tags": { "category": "biology" },
    "k": 3
  }'
```

Compare the answer when filtering by `"biology"` vs `"physics"` — the LLM only sees documents matching the filter.

### Exercise 11: Verify the hallucination guardrail

Ask a question about something that is **not** in your indexed documents:

```bash
curl -X POST http://localhost:8080/chat \
  -H "Content-Type: application/json" \
  -d '{ "question": "What is the capital of France?" }'
```

The model should respond that it doesn't have enough information to answer. Now try the same question **without** the system prompt guardrail:

```bash
curl -X POST http://localhost:8080/chat \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What is the capital of France?",
    "systemPrompt": "You are a helpful assistant. Answer any question."
  }'
```

Notice the difference — without the guardrail, the model happily answers from its training data. This is why the default system prompt is critical for a trustworthy RAG system.

### Exercise 12: Explore the Swagger UI

Open **http://localhost:8080/swagger** in your browser. Try sending requests directly from the Swagger interface. This is especially useful for exploring request/response schemas without writing curl commands.

### Exercise 13: Index a long document (observe chunking)

Paste a **long** piece of text (> 2000 characters) into a single `/documents` call. The API will automatically split it into overlapping chunks:

```bash
curl -s http://localhost:8080/documents -H "Content-Type: application/json" -d '{
  "id": "long-doc-001",
  "text": "<paste a very long text here — e.g. the first chapter of a book, a Wikipedia article, or a long technical document that exceeds 2000 characters>",
  "tags": { "category": "demo" }
}'
```

**Look at the response:**

```json
{
  "pointId": "a1b2c3d4-...",
  "totalChunks": 3,
  "chunkPointIds": [
    "a1b2c3d4-...",
    "e5f6a7b8-...",
    "c9d0e1f2-..."
  ]
}
```

Notice `totalChunks > 1` — the text was split. Each chunk is stored as a separate Qdrant point with its own embedding. Open the **Qdrant Dashboard** (http://localhost:6333/dashboard) and inspect the points. You'll see `source_doc_id`, `chunk_index`, and `total_chunks` fields in the payload.

**Why chunking matters:** Embedding models have a maximum token limit (8,191 tokens for `text-embedding-3-small`). Without chunking, texts that exceed this limit cause API errors. Chunking also improves retrieval precision — smaller, focused passages are easier to match than entire documents.

### Exercise 14: Search across chunks and observe multi-hit results

After indexing a long document (Exercise 13), search for something specific:

```bash
curl -s http://localhost:8080/search/topk -H "Content-Type: application/json" -d '{
  "queryText": "<a question about content somewhere in the long document>",
  "k": 5
}'
```

You may see **multiple results from the same source document** (they share the same `source_doc_id`). This is expected — different chunks match with different scores.

**Discussion points:**
- Why does overlap help? (Prevents losing context at chunk boundaries — a sentence split between chunks would be partially in both.)
- How does chunk size affect retrieval? (Smaller chunks = more precise retrieval but less context per hit. Larger chunks = more context but may dilute relevance.)
- What about deduplication? (In production, you might group results by `source_doc_id` and return only the best-scoring chunk per document.)

---

## Key concept: Chunking long documents

Embedding models have a **maximum input token limit** (8,191 tokens for `text-embedding-3-small`, roughly ~32,000 characters). When a document exceeds this limit, the embedding API rejects it.

The API automatically handles this with a **character-based chunker** that:

1. **Splits** the text into chunks of ≤ `MaxChunkSize` characters (default: 2,000 ≈ 500 tokens)
2. **Overlaps** consecutive chunks by `Overlap` characters (default: 200) to preserve context
3. **Respects sentence boundaries** — breaks at `.` `?` `!` or paragraph breaks when possible

### How overlap works — the sliding window

The chunker uses a **sliding window** strategy. After producing each chunk, the window advances by `chunkLength − Overlap` characters, so the **tail** of one chunk becomes the **head** of the next:

```
Original text (5 000 chars), MaxChunkSize=2000, Overlap=200:

Chunk 0:  |████████████████████|                 chars 0 – 2000
Chunk 1:           |████████████████████|        chars 1800 – 3800
Chunk 2:                    |████████████████████| chars 3600 – 5000
                   ◄── 200 ──►
                   shared overlap
```

This means any sentence near a chunk boundary appears **in full in at least one chunk**, preventing the embedding from capturing a half-sentence.

### Sentence-boundary snapping

Instead of always cutting at exactly `MaxChunkSize` characters (which could land mid-word or mid-sentence), the chunker scans backwards through the **second half** of the proposed chunk to find a natural break point. The priority order is:

1. **Paragraph break** (`\n`) — cleanest boundary
2. **Sentence ender** (`. `, `? `, `! `) — punctuation followed by whitespace
3. **Word boundary** (space) — avoids cutting mid-word
4. **Hard cut** — last resort if no boundary is found in the second half

Because boundary snapping can shorten the chunk, the **effective overlap may be larger** than the configured value — e.g. if a chunk is snapped from 2,000 to 1,850 chars, the next window overlaps by 200 from the shorter length, giving ~350 chars of shared text. The configured `Overlap` is a **minimum guarantee**, not a fixed constant.

### Safety guard

If `Overlap ≥ chunkLength` (e.g. misconfiguration), the advance would be ≤ 0 and the loop would never progress. The chunker forces `advance = chunkLength` in that case, effectively disabling overlap to prevent an infinite loop.

### How chunked documents are stored

When text is split into N chunks, the API creates **N separate Qdrant points**:

| Payload field | Value | Purpose |
|---------------|-------|---------|
| `text` | The chunk's text (not the full document) | Returned in search results |
| `source_doc_id` | Original document's point-id | Links chunks to their parent |
| `chunk_index` | `"0"`, `"1"`, `"2"` … | Ordering within the document |
| `total_chunks` | `"3"` | Total chunk count |
| `tag.*` | Inherited from parent | Every chunk is filterable by the same tags |

For short documents (≤ `MaxChunkSize`), no chunking metadata is added — the behavior is unchanged.

### Configuring chunk size

Override via `appsettings.json` or environment variables:

```json
{
  "Chunking": {
    "MaxChunkSize": 2000,
    "Overlap": 200
  }
}
```

Or via env vars: `CHUNKING_MAX_SIZE=1500` and `CHUNKING_OVERLAP=300`.

### Token-based vs character-based chunking

| Approach | Pros | Cons |
|----------|------|------|
| **Character-based** (used here) | Zero dependencies, simple, fast | ~4 chars/token approximation for English |
| **Token-based** (`Microsoft.ML.Tokenizers`) | Exact token counting, production-grade | Extra dependency (~340 KB) |

The character-based approach uses a conservative default (2,000 chars ≈ 500 tokens) to stay well under the 8,191-token limit. For production systems or non-English text, consider switching to `Microsoft.ML.Tokenizers` for precise token counting.

---

## Deterministic point-id strategy

The API generates a **deterministic UUID** for each document:

- If you provide an `id` field → `pointId = DeterministicGuid(id)`
- If you omit `id` → `pointId = DeterministicGuid(text)`

This means re-indexing the same document produces the same point-id, resulting in an **idempotent upsert** (Qdrant overwrites the existing point). No duplicates, ever.

---

## Switching models

### Embedding model

The default embedding model is `text-embedding-3-small` (1 536 dims). To use a different model:

1. Set `OPENAI_EMBEDDING_MODEL` in `docker-compose.yml` (e.g. `text-embedding-3-large`).
2. Update `EMBEDDING_DIM` to match the new model's dimensions.
3. Delete the old Qdrant collection (or use a new collection name), since Qdrant requires all vectors in a collection to have the same dimensionality.
4. Rebuild: `docker compose up --build`

No code changes needed — the model name is configurable via environment variable.

### Chat model

The default chat model is `gpt-4.1-nano` (cheapest current OpenAI model). To switch:

1. Set the `OPENAI_CHAT_MODEL` env var in `docker-compose.yml` (e.g. `gpt-4.1-mini`, `gpt-5-mini`, `gpt-5-nano`).
2. Rebuild: `docker compose up --build`

No code changes needed — the model name is configurable via environment variable.

---

## Unit tests

The test project uses **NUnit 4** + **Moq** and lives under `tests/Qdrant.Demo.Api.Tests/`.

```bash
dotnet test --verbosity normal
```

| Test class | Count | What it covers |
|-----------|-------|---------------|
| `StringExtensionsTests` | 4 | `ToDeterministicGuid` determinism, uniqueness, non-empty, version bits |
| `DateTimeExtensionsTests` | 2 | `ToUnixMs` epoch and known timestamp |
| `DocumentIndexerTests` | 9 | Point-id from explicit Id vs text fallback, idempotency, model shape, search request defaults |
| `QdrantFilterFactoryTests` | 7 | `CreateScrollFilter` + `CreateGrpcFilter` with null, empty, single, and multi-tag dictionaries |
| `TextChunkerTests` | 18 | Short/long text, overlap, sentence boundaries, paragraph breaks, edge cases, default options |
| `ChatTests` | 10 | `ChatRequest` defaults, `ChatSource`/`ChatResponse`/`SearchHit` record shapes, `PayloadKeys` constants |

---

## Extending the template

### Add streaming chat responses

The current `POST /chat` waits for the full response. You can add a `POST /chat/stream` endpoint
using `ChatClient.CompleteChatStreamingAsync()` to return Server-Sent Events (SSE) for a
real-time typing effect:

```csharp
app.MapPost("/chat/stream", async (ChatRequest req, ChatClient chatClient, ...) =>
{
    // ... embed + retrieve (same as /chat) ...
    var stream = chatClient.CompleteChatStreamingAsync(messages);
    return Results.Stream(async outputStream =>
    {
        await foreach (var update in stream)
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    await outputStream.WriteAsync(
                        Encoding.UTF8.GetBytes($"data: {part.Text}\n\n"));
                    await outputStream.FlushAsync();
                }
            }
        }
    }, "text/event-stream");
});
```

### Add multi-turn conversation

Extend `ChatRequest` with an optional `History` field (`List<ChatMessage>?`) and prepend
previous messages before the current question. This enables follow-up questions that
reference earlier answers.

### Use a different vector database

The indexing and search patterns are the same across vector DBs. Replace `QdrantClient` with your preferred provider's SDK (Pinecone, Weaviate, Milvus, ChromaDB, etc.).

### Custom metadata schemas for your domain

Adapt the tags and properties for your use case:

| Domain | Tags (filterable) | Properties (informational) |
|--------|-------------------|---------------------------|
| **Knowledge base** | `department`, `document_type`, `language` | `author`, `last_updated`, `summary` |
| **E-commerce** | `brand`, `category`, `price_range` | `product_url`, `image_url`, `description` |
| **Support tickets** | `priority`, `status`, `assigned_team` | `customer_id`, `created_at`, `resolution_notes` |
| **Code documentation** | `language`, `framework`, `api_version` | `repo_url`, `file_path`, `last_commit` |

### Improve batch ingestion performance

The current `POST /documents/batch` endpoint processes documents sequentially (one embedding call + one upsert per document). For production ingestion pipelines with hundreds or thousands of documents, consider:

1. **Parallel embedding** — batch multiple texts into a single OpenAI embedding call using `GenerateEmbeddingsAsync` (plural).
2. **Batch upsert** — Qdrant's `UpsertAsync` already accepts a list of points. Collect all points in memory first, then upsert them in one call.
3. **Chunking** — split large documents into smaller overlapping chunks before embedding, so each vector represents a focused passage rather than an entire document.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `OPENAI_API_KEY is missing` at startup | Env var not exported | `$env:OPENAI_API_KEY = "sk-..."` (PowerShell) before `docker compose up` |
| `docker compose up` hangs | Docker Desktop not running | Start Docker Desktop, wait for engine |
| `[bootstrap] attempt N failed` repeating | Qdrant not ready yet | Wait — it retries 30 times. Check `docker compose logs qdrant` |
| Search returns `[]` | No documents indexed | Index documents first with `POST /documents` |
| Tag filters return no results | No payload index on that tag field | Create a `keyword` index via Qdrant Dashboard (Exercise 5) |
| 500 with `AuthenticationException` | Invalid OpenAI key | Check key at [platform.openai.com](https://platform.openai.com/api-keys) |
