# Pre-Workshop Email Template

> Copy the text below into your email client. Replace the `[PLACEHOLDER]` values with your details.

---

**Subject:** Action Required -- Setup for the RAG Workshop on [DATE]

Hi everyone,

I'm looking forward to our **Qdrant.Demo -- RAG Workshop** on **[DATE]** at **[TIME]** in **[LOCATION / Teams link]**.

To make sure we hit the ground running, **please complete the setup steps below before the workshop**. This should take about 10 minutes.

---

## What to install

| # | Tool | Link | Why |
|---|------|------|-----|
| 1 | **Docker Desktop** | [docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop/) | Runs the Qdrant vector database |
| 2 | **.NET 10 SDK** | [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0) | Build & run the API project |
| 3 | **Git** | [git-scm.com](https://git-scm.com/) | Clone the workshop repository |
| 4 | **VS Code** *(recommended)* | [code.visualstudio.com](https://code.visualstudio.com/) | Recommended editor -- install the **C# Dev Kit** extension for the best experience |

## Pre-download the Qdrant image (~150 MB)

This avoids a download bottleneck on workshop day if everyone starts pulling at the same time.

### 1. Clone the repository

```bash
git clone https://github.com/PeterMilovcik/Qdrant.Demo.git
cd Qdrant.Demo
```

### 2. Pull the Qdrant Docker image

Make sure Docker Desktop is running, then:

```bash
docker pull qdrant/qdrant:v1.16.3
```

### 3. Verify your setup

```bash
docker --version                            # Docker version 27.x or later
dotnet --version                            # 10.0.101 or later
docker images qdrant/qdrant --format "table {{.Repository}}\t{{.Tag}}"   # Should show v1.16.3
```

If all three checks pass, you're ready!

## OpenAI API key

The workshop uses **OpenAI** for embeddings and chat (models: `text-embedding-3-small` and `gpt-4o-mini`). **[I will provide a shared API key on workshop day / Please bring your own OpenAI API key]** -- delete whichever option does not apply.

If you want to bring your own key, you can create one at [platform.openai.com/api-keys](https://platform.openai.com/api-keys). Expected cost for the full workshop is under $1.

---

## What we'll build

Over **~2.5 hours**, we'll build a complete **Retrieval-Augmented Generation (RAG)** API from scratch:

- Store documents as vectors in a Qdrant vector database
- Search by meaning (not keywords) using cosine similarity
- Add metadata filtering
- Wire up OpenAI to answer questions grounded in your documents
- Handle long documents with text chunking

No heavy downloads on workshop day -- just Docker, .NET, and an API key.

---

## Questions?

Reply to this email or ping me on MS Teams.

See you on **[DATE]**!

[YOUR NAME]

---

> *This email was generated from the [materials/](https://github.com/PeterMilovcik/Qdrant.Demo/tree/main/materials) folder in the workshop repository.*
