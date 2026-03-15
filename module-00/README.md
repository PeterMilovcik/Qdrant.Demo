# Module 0 — Setup & Orientation

> **~20 min** · No LLM needed yet · No code to write

## Learning objective

By the end of this module you will have:

- Qdrant running in Docker
- A bare .NET API skeleton that connects to Qdrant and creates a collection at startup
- Swagger UI open in your browser
- The Qdrant Dashboard open for inspecting your data

---

## What you'll need open

Keep these three things visible throughout the workshop:

1. **Terminal** — to run commands
2. **Browser tab 1** — Qdrant Dashboard at `http://localhost:6333/dashboard`
3. **Browser tab 2** — Swagger UI at `http://localhost:8080/swagger` (when running locally)

---

## Concepts introduced

### Qdrant — a vector database

**Qdrant** is an open-source vector database. It stores **points** — each point has:

- A **unique id** (UUID or integer)
- A **vector** (an array of floats — we'll use 1536 dimensions)
- An optional **payload** (key/value metadata)

Points live inside **collections**. A collection is like a database table — all vectors in a collection must have the same dimensionality.

In this module, we just create the empty collection. We'll add points starting in Module 1.

### The API skeleton

The `Program.cs` in this module is minimal:

- Reads Qdrant connection config from `appsettings.json` (with env-var overrides)
- Registers `QdrantClient` (the gRPC client for Qdrant)
- Starts `QdrantBootstrapper` — a background service that creates the `documents` collection at startup (with retries, since Qdrant may not be ready immediately)
- Exposes Swagger UI and an info endpoint (`GET /api/info`); a hidden health check (`GET /health`) is also available but excluded from Swagger

No LLM, no embeddings, no documents yet — just infrastructure.

---

## Step 1 — Start Qdrant

From this module's folder, start the Qdrant container:

```bash
cd module-00
```

```bash
docker compose up -d
```

Wait a few seconds, then verify Qdrant is running:

```bash
curl http://localhost:6333/healthz
```

You should see `healthz check passed` (or similar). Open the **Qdrant Dashboard** in your browser:

👉 **http://localhost:6333/dashboard**

You'll see an empty dashboard — no collections yet.

## Step 2 — Run the API

Set the API port (keep this variable set for the rest of the workshop — every module uses port 8080):

```powershell
# PowerShell
$env:ASPNETCORE_URLS = "http://localhost:8080"
```

```bash
# Linux/macOS
export ASPNETCORE_URLS="http://localhost:8080"
```

Then run the API:

```bash
dotnet run --project src/Qdrant.Demo.Api
```

You should see output like:

```
[bootstrap] Collection 'documents' ready.
info: Microsoft.Hosting.Lifetime[14]
  Now listening on: http://localhost:8080
```

## Step 3 — Explore

### Swagger UI

Open `http://localhost:8080/swagger`.

You'll see two endpoints:
- `GET /api/info` — returns service info (Qdrant config, collection name, embedding dimensions)
- `GET /health` — hidden from Swagger but reachable directly

Try calling `GET /api/info` from Swagger — you should see the configuration JSON.

### Qdrant Dashboard

Go back to **http://localhost:6333/dashboard**. You should now see a `documents` collection listed. Click on it to explore — it's empty (0 points), but the collection is ready.

**Notice:**
- **Dimension:** 1536 (matching `text-embedding-3-small`)
- **Distance:** Cosine (we'll use cosine similarity for searching)

## Step 4 — Inspect the code

Open these files and read through them:

| File | What to notice |
|------|---------------|
| `Program.cs` | How config values are read with env-var fallbacks |
| `Endpoints/InfoEndpoints.cs` | The `MapInfoEndpoints` extension method — `GET /api/info` and `GET /health` |
| `Services/QdrantBootstrapper.cs` | The retry loop (30 attempts, 1s apart) — tolerates Qdrant starting slowly |
| `appsettings.json` | Default Qdrant connection values |
| `docker-compose.yml` | Only Qdrant — the API runs locally via `dotnet run` |

---

## Exercises

### Exercise 0.1 — Verify the collection exists

Open the Qdrant Dashboard and confirm:
- The `documents` collection exists
- It has 0 points
- The vector dimension is 1536
- The distance metric is Cosine

### Exercise 0.2 — Call the info endpoint

Using curl or Swagger, call `GET /api/info` and verify the response matches your configuration:

```bash
curl http://localhost:8080/api/info
```

Expected response:

```json
{
  "service": "Qdrant.Demo.Api",
  "qdrant": {
    "host": "localhost",
    "http": 6333,
    "grpc": 6334,
    "collection": "documents",
    "embeddingDim": 1536
  }
}
```

### Exercise 0.3 — Restart and observe idempotency

Stop the API (`Ctrl+C`) and start it again. The bootstrapper will try to create the collection again — but since it already exists, the `AlreadyExists` gRPC exception is caught silently. Check the logs: you should see `Collection 'documents' ready.` without errors.

---

## ✅ Checkpoint

At this point you have:

- [x] Qdrant running in Docker with an empty `documents` collection
- [x] A .NET API skeleton with Swagger UI
- [x] The Qdrant Dashboard showing your collection
- [x] Understanding of: collections, points (conceptually), and the bootstrapper pattern

## 🃏 Flashcards

Test your understanding of this module's key concepts. Click a question to reveal the answer.

<details>
<summary>What is a vector database?</summary>

A database optimized for storing and searching **high-dimensional vectors** (arrays of floats). Instead of matching exact values like a traditional database, it finds items that are *similar* based on the distance between their vectors.

</details>

<details>
<summary>What is a Qdrant "point"?</summary>

The basic storage unit in Qdrant. A point consists of three things: a **unique ID** (UUID or integer), a **vector** (array of floats), and an optional **payload** (key-value metadata).

</details>

<details>
<summary>What is a "collection" in Qdrant, and what constraint does it enforce?</summary>

A collection is like a database table — it groups related points together. All vectors in a collection must have the **same dimensionality** (e.g., all 1536-dimensional).

</details>

<details>
<summary>Why does the QdrantBootstrapper use a retry loop?</summary>

Qdrant may not be fully ready when the API starts (especially in Docker). The bootstrapper retries up to 30 times (1 second apart) to create the collection, tolerating slow container startup.

</details>

<details>
<summary>What distance metric does this workshop use, and what does it measure?</summary>

**Cosine similarity.** It measures the angle between two vectors — values range from 0 (unrelated) to 1 (identical meaning). It compares the *direction* of vectors, not their magnitude.

</details>

<details>
<summary>What happens if the bootstrapper tries to create a collection that already exists?</summary>

The `AlreadyExists` gRPC exception is caught silently, and the bootstrapper logs "Collection ready." The operation is **idempotent** — safe to repeat without side effects.

</details>

<details>
<summary>Why is the API run locally with <code>dotnet run</code> instead of in Docker?</summary>

The API runs locally for faster development iteration — you can edit code and restart quickly. Only Qdrant runs in Docker because it's infrastructure (a database), not code you're actively developing.

</details>

## 🔧 Troubleshooting

### NuGet restore fails — corporate / custom artifact source

If you're working in a corporate environment that uses a custom NuGet artifact repository (e.g., Artifactory, Azure DevOps Artifacts) instead of the default `nuget.org`, the `dotnet restore` step may fail because the required packages aren't available in your configured source.

**Option A — Enable an existing `nuget.org` source**

Your machine may already have a `nuget.org` source configured but disabled. Run:

```bash
dotnet nuget enable source nuget.org
```

This re-enables the built-in `nuget.org` source so that `dotnet restore` can pull packages from it alongside your corporate feed.

**Option B — Add `nuget.org` as a new source**

If no `nuget.org` source exists at all, add it manually:

```bash
dotnet nuget add source https://api.nuget.org/v3/index.json --name "nuget.org"
```

This registers the official NuGet Gallery as an additional package source. Packages that aren't found in your corporate feed will now be resolved from `nuget.org`.

> **Tip:** You can list your currently configured sources with `dotnet nuget list source` to see which feeds are active before making changes.

After running either command, retry `dotnet restore` (or `dotnet run`, which restores automatically).

---

## 🧹 Clean Up

Before moving to the next module, stop everything started in this module:

1. **Stop the local API** — press `Ctrl+C` in the terminal where `dotnet run` is running
2. **Stop Docker containers** — from the `module-00` directory:

```bash
docker compose down
```

This ensures ports 8080, 6333, and 6334 are free for the next module.

**Next →** [Module 1 — Index](../module-01/README.md)
