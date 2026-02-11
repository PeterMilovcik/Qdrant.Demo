# Workshop Materials

This folder contains resources for **workshop organizers** to help participants prepare before workshop day.

## Contents

| File | Purpose |
|------|---------|
| [pre-workshop-email.md](pre-workshop-email.md) | Ready-to-send email template -- copy into Outlook/Gmail, fill in the `[PLACEHOLDER]` values, and send to participants ~1 week before the workshop |

## Why pre-setup matters

The workshop needs Docker (for Qdrant) and an **OpenAI API key** (for embeddings + chat). Having participants install prerequisites and pull the Qdrant image **before** the workshop avoids a bandwidth bottleneck on the day.

| Pre-download | Size |
|-------------|------|
| Docker image (`qdrant/qdrant:v1.16.3`) | ~150 MB |

## Participant setup (include in your pre-workshop email)

### 1. Install prerequisites

| Tool | Link |
|------|------|
| Docker Desktop | [docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop/) |
| .NET 10 SDK | [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Git | [git-scm.com](https://git-scm.com/) |

### 2. Pull the Qdrant Docker image

```bash
docker pull qdrant/qdrant:v1.16.3
```

### 3. Set the OpenAI API key

Each participant needs an OpenAI API key. Set it as an environment variable before running any module:

```powershell
# PowerShell
$env:OPENAI_API_KEY = "sk-..."
```

```bash
# bash / zsh
export OPENAI_API_KEY="sk-..."
```

> **Workshop organizer tip:** You can create a single project-scoped API key at [platform.openai.com/api-keys](https://platform.openai.com/api-keys) with a spending limit and share it with participants. Expected cost is ~$0.50 per participant for the full workshop.

### 4. Verify

```bash
docker images qdrant/qdrant --format "table {{.Repository}}\t{{.Tag}}"   # should show v1.16.3
dotnet --version                     # should show 10.0.x
echo $OPENAI_API_KEY                 # should show sk-...
```

## Corporate network notes

This setup is **corporate-network friendly** by design:

- **Docker** pulls `qdrant/qdrant` from **Docker Hub** -- works through corporate proxies that do SSL inspection
- The .NET API calls the **OpenAI API** via HTTPS -- .NET uses the **Windows certificate store**, so corporate CA certificates are trusted automatically
- No local AI model downloads needed -- no blocked registries
- The API runs via `dotnet run` natively -- no containers with SSL issues
