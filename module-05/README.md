# Module 5 — User Interface

> **~20 min** · Builds on [Module 4](../module-04/README.md) · **Bonus module**

## Learning objective

By the end of this module you will have:

- Served a **static frontend** from ASP.NET Core using `UseStaticFiles()`
- Interacted with every workshop endpoint through a **visual UI** instead of Swagger
- Experienced RAG from the **end-user perspective** — ask a question, see the answer and its sources
- Compared search strategies (Top-K vs Threshold vs Metadata) **side by side**

---

## Concepts introduced

### Static file middleware

ASP.NET Core can serve HTML, CSS, JS, and images straight from a `wwwroot/` folder — no separate web server needed. Two middleware calls make it work:

```csharp
app.UseDefaultFiles();   // maps "/" → "/index.html"
app.UseStaticFiles();    // serves everything in wwwroot/
```

`UseDefaultFiles()` must come **before** `UseStaticFiles()`. Together they mean: visiting `http://localhost:8080/` serves `wwwroot/index.html`, while API routes like `/api/info`, `/chat`, and `/search/topk` continue to work as before.

### Single-file frontend

The entire UI is a single `index.html` (no build step, no npm, no bundler). It uses:

- [**Pico.css**](https://picocss.com) — a classless CSS framework loaded from CDN (~10 KB). Semantic HTML elements (`<article>`, `<nav>`, `<details>`) get clean styling automatically, including automatic dark/light mode based on your OS preference.
- **Vanilla JavaScript** — plain `fetch()` calls to the API endpoints. No framework, no transpiler.

---

## What changed from Module 4

| New file | Purpose |
|----------|---------|
| `wwwroot/index.html` | Single-file frontend: Chat, Search, Documents, and Status tabs |

| Changed file | What changed |
|-------------|-------------|
| `Program.cs` | Added `UseDefaultFiles()` + `UseStaticFiles()` middleware so the root URL serves the frontend |

### Code walkthrough

#### Static file setup — [`Program.cs`](src/Qdrant.Demo.Api/Program.cs)

Three lines make the frontend work:

```csharp
// ---- static files (serves wwwroot/index.html as the frontend) ----
app.UseDefaultFiles();   // maps "/" → "/index.html"
app.UseStaticFiles();
```

#### Frontend structure — [`wwwroot/index.html`](src/Qdrant.Demo.Api/wwwroot/index.html)

The single HTML file is organized into four tabs:

| Tab | Endpoints used | What it shows |
|-----|---------------|---------------|
| **Chat** | `POST /chat` | Conversation-style RAG interface with answer + expandable sources |
| **Search** | `POST /search/topk`, `/search/threshold`, `/search/metadata` | Three search modes with score bars, chunk badges, and payload details |
| **Documents** | `POST /documents`, `POST /documents/batch` | Single and batch document indexing with chunk count feedback |
| **Status** | `GET /api/info`, `GET /health` | Config grid and auto-refreshing health indicator |

A reusable **tag chip widget** (key + value input → dismissible pills) appears in every form that accepts tags, keeping the UI consistent across all endpoints.

---

## Step 1 — Start Qdrant and run the API

```bash
cd module-05
```

```bash
docker compose up -d
```

Then run the API locally:

```bash
dotnet run --project src/Qdrant.Demo.Api
```

## Step 2 — Open the frontend

Visit **http://localhost:8080/** in your browser.

You should see the **Chat** tab with an empty conversation area. The navigation bar at the top has four tabs: **Chat**, **Search**, **Documents**, and **Status**.

> **Note:** Swagger UI is still available at **http://localhost:8080/swagger** if you need it.

## Step 3 — Check the Status tab

Click the **Status** tab. You should see:

- A green **Healthy** indicator (auto-refreshes every 15 seconds)
- A configuration grid showing your Qdrant connection, embedding model, chat model, and chunking settings

If the dot is red, make sure Qdrant is running (`docker compose up -d`) and the API started without errors.

## Step 4 — Index some documents

Click the **Documents** tab.

### Single document

1. In the **Text** field, paste:

```
Photosynthesis converts sunlight into chemical energy in plants.
```

2. Under **Tags**, type `category` as key, `science` as value, and click **Add**
3. Click **Index Document**

You should see a green result card showing the Point ID and **1 chunk**.

### Long document (chunking)

1. Clear the Text field and paste a long text (e.g., the coffee history article from Module 4 — over 3000 characters)
2. Add a tag: `category` = `history`
3. Click **Index Document**

This time you should see **4 chunks** with an expandable list of chunk IDs.

### Batch upload

Scroll down to **Batch upload**, paste:

```json
[
  { "id": "planet-1", "text": "Mercury is the closest planet to the Sun and has no atmosphere.", "tags": { "category": "science" } },
  { "id": "planet-2", "text": "Venus is the hottest planet due to its thick carbon dioxide atmosphere.", "tags": { "category": "science" } },
  { "id": "planet-3", "text": "Mars has the largest volcano in the solar system, Olympus Mons.", "tags": { "category": "science" } }
]
```

Click **Index Batch**. You should see: **Total: 3, Succeeded: 3**.

## Step 5 — Search your documents

Click the **Search** tab.

### Top-K search

1. The **Top-K** mode is selected by default
2. Type: `How do plants produce energy?`
3. Leave K at 5 and click **Search**
4. Results appear with score bars — the photosynthesis document should rank highest

### Threshold search

1. Click the **Threshold** button in the mode selector
2. Type the same query: `How do plants produce energy?`
3. Drag the **Score threshold** slider to `0.50`
4. Click **Search**
5. Only documents above the 0.50 threshold appear — try dragging the slider to see results appear and disappear

### Metadata browse

1. Click the **Metadata** button
2. Add a tag: `category` = `science`
3. Click **Browse**
4. All science-tagged documents appear (no similarity scores — this is a tag-only query)

## Step 6 — Chat with your documents

Click the **Chat** tab.

1. Type: `How do plants produce energy?` and click **Send**
2. The assistant responds with an answer grounded in your indexed documents
3. Click the **▸ sources used** expander below the answer to see which chunks were retrieved and their scores

Try another question:

```
What is the hottest planet and why?
```

The RAG pipeline retrieves the Venus document and generates an answer from it.

### Advanced settings

Click **⚙ Advanced settings** to reveal:

- **K** — adjust how many chunks are retrieved
- **Score threshold** — filter out low-relevance chunks
- **Tags** — restrict retrieval to specific categories
- **System prompt** — override the default prompt to change the assistant's behavior

Try adding a tag `category` = `history` and asking about coffee — only the coffee history chunks will be used as context.

---

## Exercises

### Exercise 5.1 — Dark mode

If your OS is in light mode, switch to dark mode (or vice versa). Reload the page — the UI adapts automatically thanks to Pico.css's `data-theme="auto"`.

### Exercise 5.2 — Custom system prompt

In the Chat tab, open Advanced settings and set the system prompt to:

```
You are a pirate. Answer questions using pirate language, but still base your answers on the provided context documents.
```

Ask a question and see how the tone changes while the facts stay grounded.

---

## ✅ Final Checkpoint

Congratulations — you've completed the entire workshop! Here's everything you built:

| Module | Feature |
|--------|---------|
| 0 | Setup — Qdrant connection, Swagger, health check |
| 1 | Index — embeddings, Qdrant points, batch indexing |
| 2 | Retrieval — similarity search, metadata, filtering |
| 3 | Generation — RAG pipeline, custom prompts, filtered chat |
| 4 | Chunking — text splitting, sentence boundaries, overlap |
| 5 | User Interface — static frontend for every endpoint |

### What to explore next

- **Token-aware chunking** — Replace the character-based chunker with `Microsoft.ML.Tokenizers` for exact token counts
- **Streaming chat** — Use `IChatClient.GetStreamingResponseAsync` for real-time token streaming
- **Named vectors** — Store multiple embedding models in the same collection
- **Hybrid search** — Combine dense (semantic) and sparse (keyword) vectors
- **Authentication** — Add API keys or OAuth to protect the endpoints

---

## 🧹 Clean Up

When you're done exploring, stop everything:

1. **Stop the local API** — press `Ctrl+C` in the terminal where `dotnet run` is running
2. **Stop Docker containers** — from the `module-05` directory:

```bash
docker compose down
```

### Full cleanup (optional)

If you want to remove **everything** created during the workshop:

1. **Remove all Qdrant data** — add `-v` to also delete the Docker volumes:

```bash
docker compose down -v
```

2. **Remove the Qdrant Docker image** (frees ~200 MB):

```bash
docker rmi qdrant/qdrant:v1.16.3
```

3. **Clear build artifacts** — from the repo root:

```powershell
# PowerShell — remove all bin/ and obj/ folders
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force
```

```bash
# Linux/macOS
find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +
```

4. **Unset environment variables** — if you set them during the workshop:

```powershell
# PowerShell
Remove-Item Env:ASPNETCORE_URLS -ErrorAction SilentlyContinue
Remove-Item Env:OPENAI_API_KEY -ErrorAction SilentlyContinue
Remove-Item Env:CHUNKING_MAX_SIZE -ErrorAction SilentlyContinue
Remove-Item Env:CHUNKING_OVERLAP -ErrorAction SilentlyContinue
```

```bash
# Linux/macOS
unset ASPNETCORE_URLS OPENAI_API_KEY CHUNKING_MAX_SIZE CHUNKING_OVERLAP
```

5. **Revert NuGet source changes** — only if you modified your NuGet sources in [Module 00](../module-00/README.md#-troubleshooting):

   If you **enabled** a previously disabled `nuget.org` source, disable it again:

   ```bash
   dotnet nuget disable source nuget.org
   ```

   If you **added** `nuget.org` as a new source, remove it:

   ```bash
   dotnet nuget remove source nuget.org
   ```

   > **Tip:** Run `dotnet nuget list source` to check your current state before making changes.

6. **Delete the repo** — if you no longer need the workshop files:

```powershell
# PowerShell — from the parent directory
Remove-Item -Path Qdrant.Demo -Recurse -Force
```

```bash
# Linux/macOS
rm -rf Qdrant.Demo
```

---

**← Back to** [Root README](../README.md)
