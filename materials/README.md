# Workshop Materials

This folder contains resources for **workshop organizers** to help participants prepare before workshop day.

## Contents

| File / Folder | Purpose |
|------|---------|
| [pre-workshop-email.md](pre-workshop-email.md) | Ready-to-send email template — update the `[PLACEHOLDER]` values and send to participants **1–2 weeks** before the workshop |
| [docker-alternatives.md](docker-alternatives.md) | Free alternatives to Docker Desktop for companies that require a licence |
| [slides/](slides/) | *(Optional)* Slidev presentation — project the slides during the workshop (`cd slides && npm install && npm run dev`). Each module's README is detailed and comprehensive enough to run the workshop without slides |

## Why pre-setup matters

The workshop needs Docker (for Qdrant) and an **Azure OpenAI resource** (for embeddings + chat). Having participants install prerequisites and pull the Qdrant image **before** the workshop avoids a bandwidth bottleneck on the day.

| Pre-download | Size |
|-------------|------|
| Docker image (`qdrant/qdrant:v1.16.3`) | ~150 MB |

## Organizing the workshop

### Send the invitation early

Use [pre-workshop-email.md](pre-workshop-email.md) as a starting point for your meeting invitation. Replace the `[PLACEHOLDER]` values with your details and send it **1–2 weeks before** the workshop date — this gives participants enough time to install prerequisites, pull the Docker image, and sort out any corporate proxy issues.

### In-person is best

This workshop works best **in person** in a meeting room. Book a room with a **projector or large TV screen** so you can walk through modules together. A few snacks, coffee, or sweets on the table go a long way toward keeping energy levels up during a ~3-hour session.

### Schedule an online meeting too

Even for an in-person workshop, create an **online meeting** (e.g., MS Teams) alongside the room booking:

- Participants who have difficulty seeing the projector or TV can **join the online meeting on their laptops** and see the shared screen up close
- Use the **meeting chat** to share useful links, the Azure OpenAI endpoint and API key, code snippets, or any other materials — participants can copy-paste directly instead of retyping from the screen
- Remote participants can still follow along if needed

> **Tip:** At the start of the session, post the Azure OpenAI endpoint, API key, and the repository URL in the meeting chat so everyone can grab them instantly.

## Running the presentation

The workshop includes an **optional Slidev presentation** (~80 slides) that walks through each module with code examples, architecture diagrams, and exercises. The slides are **not required** — each module's README already contains detailed explanations, step-by-step instructions, and exercises that are comprehensive enough to run the workshop without slides.

### Start the presentation

```bash
cd materials/slides
npm install        # first time only
npm run dev        # or: ./node_modules/.bin/slidev slides.md --open
```

The slides will open at **http://localhost:3030** in presenter mode.

> **Offline option**: A PDF export (`Qdrant.Demo.workshop.slides.pdf`) is available in the `materials/slides/` folder if you need to present without running Slidev.

### Presentation tips

- **Module structure**: Each module has concept slides followed by code walkthrough slides
- **Live coding**: The slides complement hands-on coding — show the slide, then code along with participants
- **Exercises**: Each module ends with "Try It" slides — give participants 5-10 minutes to experiment
- **Navigation**: Use arrow keys or click; press `O` for slide overview; press `F` for fullscreen
- **Dual screen**: Project slides on main screen, keep terminal/IDE on laptop screen

### Slidev shortcuts

| Key | Action |
|-----|--------|
| `→` / `←` | Next/previous slide |
| `Space` | Next slide |
| `O` | Overview mode (grid view) |
| `F` | Fullscreen |
| `D` | Dark/light mode |
| `Esc` | Exit modes |

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

### 3. Set the Azure OpenAI environment variables

Each participant needs access to an Azure OpenAI resource. Set the endpoint and API key as environment variables before running any module:

```powershell
# PowerShell
$env:AZURE_OPENAI_ENDPOINT = "https://your-resource.openai.azure.com"
$env:AZURE_OPENAI_API_KEY = "your-azure-openai-api-key"
```

```bash
# bash / zsh
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
export AZURE_OPENAI_API_KEY="your-azure-openai-api-key"
```

> **Workshop organizer tip:** You can create a shared Azure OpenAI resource and distribute the endpoint and API key to participants. Expected cost is ~$0.50 per participant for the full workshop.

### 4. Verify

```bash
docker images qdrant/qdrant --format "table {{.Repository}}\t{{.Tag}}"   # should show v1.16.3
dotnet --version                     # should show 10.0.x
echo $AZURE_OPENAI_ENDPOINT          # should show https://your-resource.openai.azure.com
```

## Corporate network notes

This setup is **corporate-network friendly** by design:

- **Docker** pulls `qdrant/qdrant` from **Docker Hub** -- works through corporate proxies that do SSL inspection
- The .NET API calls the **Azure OpenAI API** via HTTPS — .NET uses the **Windows certificate store**, so corporate CA certificates are trusted automatically
- No local AI model downloads needed -- no blocked registries
- The API runs via `dotnet run` natively -- no containers with SSL issues
