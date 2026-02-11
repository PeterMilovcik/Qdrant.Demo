# Workshop Materials

This folder contains resources for **workshop organizers** to help participants prepare before workshop day.

## Contents

| File | Purpose |
|------|---------|
| [pre-workshop-email.md](pre-workshop-email.md) | Ready-to-send email template -- copy into Outlook/Gmail, fill in the `[PLACEHOLDER]` values, and send to participants ~1 week before the workshop |

## Why pre-download matters

The workshop needs to download the Qdrant Docker image and two Ollama models:

| Download | Size |
|----------|------|
| Docker image (`qdrant/qdrant:v1.16.3`) | ~150 MB |
| Ollama embedding model (`nomic-embed-text`) | ~274 MB |
| Ollama chat model (`llama3.2`) | ~2 GB |

If 10+ participants start downloading simultaneously on workshop day, the shared network bandwidth becomes a bottleneck. Have participants complete the setup steps **before** the workshop.

## Participant setup (include in your pre-workshop email)

### 1. Install prerequisites

| Tool | Link |
|------|------|
| Docker Desktop | [docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop/) |
| .NET 10 SDK | [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Ollama | [ollama.com](https://ollama.com/) |
| Git | [git-scm.com](https://git-scm.com/) |

### 2. Pull the Qdrant Docker image

```bash
docker pull qdrant/qdrant:v1.16.3
```

### 3. Pull the Ollama models

```bash
ollama pull nomic-embed-text
ollama pull llama3.2
```

### 4. Verify

```bash
docker images | grep qdrant         # should show qdrant/qdrant v1.16.3
ollama list                          # should show nomic-embed-text and llama3.2
dotnet --version                     # should show 10.0.x
```

## Corporate network notes

This setup is **corporate-network friendly** by design:

- **Docker** pulls `qdrant/qdrant` from **Docker Hub** -- works through corporate proxies that do SSL inspection
- **Ollama** runs **natively** on the host OS and uses the **host certificate store** -- corporate CA certificates are already trusted, so `ollama pull` works through the proxy
- The .NET API runs via `dotnet run` using **host certificates** -- no SSL issues
- **No access to `registry.ollama.ai` from inside Docker containers** is needed
