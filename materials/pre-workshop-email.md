# Pre-Workshop Email Template

> Copy the text below into your email client. Replace the `[PLACEHOLDER]` values with your details.

---

**Subject:** Action Required -- Setup for the RAG Workshop on [DATE]

Hi everyone,

I'm looking forward to our **Qdrant.Demo -- RAG Workshop** on **[DATE]** at **[TIME]** in **[LOCATION / Teams link]**.

To make sure we hit the ground running, **please complete the setup steps below before the workshop**. The downloads total about **2.5 GB** -- if everyone starts downloading at the same time on workshop day, we'll spend the first 30 minutes staring at progress bars.

---

## What to install

| # | Tool | Link | Why |
|---|------|------|-----|
| 1 | **Docker Desktop** | [docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop/) | Runs the Qdrant vector database |
| 2 | **.NET 10 SDK** | [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0) | Build & run the API project |
| 3 | **Ollama** | [ollama.com](https://ollama.com/) | Runs local AI models (embeddings + chat) -- one-click installer |
| 4 | **Git** | [git-scm.com](https://git-scm.com/) | Clone the workshop repository |
| 5 | **VS Code** *(recommended)* | [code.visualstudio.com](https://code.visualstudio.com/) | Recommended editor -- install the **C# Dev Kit** extension for the best experience |

## Pre-download everything (~2.5 GB)

This is the **most important step** -- it avoids the download bottleneck on workshop day.

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

### 3. Pull the Ollama models

Ollama should be running after installation (it starts as a background service). Pull the two models we'll use:

```bash
ollama pull nomic-embed-text
ollama pull llama3.2
```

The first model is ~274 MB (embeddings), the second is ~2 GB (chat). This may take a few minutes.

### 4. Verify your setup

```bash
docker --version          # Docker version 27.x or later
dotnet --version          # 10.0.101 or later
ollama list               # Should show nomic-embed-text and llama3.2
docker images | grep qdrant   # Should show qdrant/qdrant v1.16.3
```

If all four checks pass, you're ready!

---

## What we'll build

Over **~2.5 hours**, we'll build a complete **Retrieval-Augmented Generation (RAG)** API from scratch:

- Store documents as vectors in a Qdrant vector database
- Search by meaning (not keywords) using cosine similarity
- Add metadata filtering
- Wire up a local LLM (Ollama) to answer questions grounded in your documents
- Handle long documents with text chunking

Everything runs **100% locally** -- no cloud APIs, no API keys, no usage fees.

---

## Questions?

Reply to this email or ping me on MS Teams.

See you on **[DATE]**!

[YOUR NAME]

---

> *This email was generated from the [materials/](https://github.com/PeterMilovcik/Qdrant.Demo/tree/main/materials) folder in the workshop repository.*
