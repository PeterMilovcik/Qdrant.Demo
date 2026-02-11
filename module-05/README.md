# Module 5 — RAG Chat

> **~20 min** · Builds on [Module 4](../module-04/README.md)

## Learning objective

By the end of this module you will have:

- Understood **Retrieval-Augmented Generation (RAG)** — the core pattern behind AI-powered Q&A
- Built a `POST /chat` endpoint that retrieves documents, assembles context, and generates an answer
- Seen how the LLM is grounded in your indexed documents and refuses to hallucinate

---

## Concepts introduced

### What is RAG?

**Retrieval-Augmented Generation** is a two-step pattern:

1. **Retrieve** — embed the user's question, search the vector database for similar documents
2. **Generate** — feed those documents as context to a Large Language Model (LLM), which writes a grounded answer

```
Question → Embed → Search Qdrant → Top-K documents
                                          ↓
                   LLM ← System prompt + Context + Question
                                          ↓
                                      Answer
```

### Why RAG instead of just asking the LLM?

LLMs have a training cutoff and don't know about **your** data. RAG bridges this gap:

| Without RAG | With RAG |
|-------------|----------|
| LLM answers from training data only | LLM answers from **your documents** |
| May hallucinate facts | Grounded — cites what's actually indexed |
| Generic answers | Specific to your domain |

### The system prompt

The system prompt tells the LLM the rules:

> *"Answer the user's question based **only** on the provided context documents. If the context does not contain enough information to answer, say so clearly — do not make up facts."*

This is a hard-coded default in Module 5. In Module 6 you'll make it customizable.

---

## What changed from Module 4

| New file | Purpose |
|----------|---------|
| `Endpoints/ChatEndpoints.cs` | `POST /chat` — embed question → search → build context → call OpenAI → return answer + sources |

| Changed file | What changed |
|-------------|-------------|
| `Models/Requests.cs` | Added `ChatRequest(Question, K)`, `ChatResponse`, `ChatSource` records |
| `Program.cs` | Added `ChatClient` registration, `chatModel` config, `MapChatEndpoints()` |
| `docker-compose.yml` | Added `OPENAI_CHAT_MODEL` environment variable |

---

## Step 1 — Start Qdrant and run the API

```bash
cd module-05
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

## Step 2 — Index some knowledge

```bash
curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "bio-001",
    "text": "Photosynthesis is the process by which green plants convert sunlight into chemical energy, producing glucose and oxygen as byproducts.",
    "tags": { "category": "biology" }
  }'

curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "bio-002",
    "text": "DNA replication is the biological process of producing two identical copies of DNA from one original DNA molecule. It occurs during the S phase of the cell cycle.",
    "tags": { "category": "biology" }
  }'

curl -X POST http://localhost:8080/documents \
  -H "Content-Type: application/json" \
  -d '{
    "id": "phys-001",
    "text": "Quantum entanglement is a phenomenon where two particles become linked, so the quantum state of one instantly influences the other, regardless of distance.",
    "tags": { "category": "physics" }
  }'
```

## Step 3 — Ask a question

```bash
curl -X POST http://localhost:8080/chat \
  -H "Content-Type: application/json" \
  -d '{"question": "How do plants produce energy from sunlight?"}'
```

The response includes:
- **answer** — a generated paragraph grounded in the photosynthesis document
- **sources** — the documents used as context, with their similarity scores

```json
{
  "answer": "Plants produce energy through photosynthesis, a process where...",
  "sources": [
    {
      "id": "abc-123...",
      "score": 0.89,
      "textSnippet": "Photosynthesis is the process by which green plants..."
    }
  ]
}
```

## Step 4 — Ask something not in the index

```bash
curl -X POST http://localhost:8080/chat \
  -H "Content-Type: application/json" \
  -d '{"question": "What is the best pizza recipe?"}'
```

The LLM should respond with something like *"The provided context does not contain information about pizza recipes"* — because the system prompt forbids making up facts.

---

## Exercises

### Exercise 5.1 — Adjust K

Try `"k": 1` — the model gets less context and may give a shorter answer. Try `"k": 10` — with only 3 documents, it still gets all of them.

### Exercise 5.2 — Ask a cross-domain question

```bash
curl -X POST http://localhost:8080/chat \
  -H "Content-Type: application/json" \
  -d '{"question": "Compare biological replication with quantum physics"}'
```

The model should pull from both biology and physics documents.

### Exercise 5.3 — Inspect the sources

The `sources` array shows exactly which documents the LLM used. Check the scores — higher scores mean the document was more relevant to the question.

### Exercise 5.4 — Run the tests

```bash
cd module-05
dotnet test
```

You should see **27 tests passed**.

---

## ✅ Checkpoint

At this point you have:

- [x] A working `POST /chat` endpoint — full RAG pipeline
- [x] Retrieval + generation in a single API call
- [x] LLM grounded in your indexed documents (no hallucination)
- [x] Understanding of: RAG pattern, system prompts, context assembly, ChatClient

**Next →** [Module 6 — Advanced Chat](../module-06/README.md)
