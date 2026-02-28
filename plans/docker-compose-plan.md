# Docker packaging plan for SimpleMessenger

## Key findings
- App listens on `Server:Port` from configuration; default `5237`.
- JSON persistence uses files under `AppDomain.CurrentDomain.BaseDirectory`:
  - `message_history.json`
  - `chat_rooms.json`
  - `chat_participants.json`
- Decision: use `aspnet:10.0-alpine` and persist JSON under `/app/data` (code change required).

## Proposed Dockerfile (production)
- Multi-stage build:
  1. `mcr.microsoft.com/dotnet/sdk:10.0` for restore/build/publish.
  2. `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` for runtime.
- Use build cache: copy `SimpleMessenger.csproj` first, `dotnet restore`, then copy remaining sources.
- Publish `dotnet publish -c Release -o /app/publish -p:UseAppHost=false`.
- Create non-root user and run as that user.
- Set `ASPNETCORE_URLS=http://0.0.0.0:5237` and `ASPNETCORE_ENVIRONMENT=Production`.
- Create `/app/data` and mount a named volume there.

## Proposed docker-compose.yml
- Single service `app` with:
  - Build from local `Dockerfile`.
  - Ports: `5237:5237`.
  - Environment:
    - `ASPNETCORE_ENVIRONMENT=Production`
    - `ASPNETCORE_URLS=http://0.0.0.0:5237`
    - `Server__Port=5237` (override config consistently).
    - `DATA_DIR=/app/data` (used by repository to store JSON).
  - Volume mapping: named volume to `/app/data`.
  - `restart: unless-stopped`.
- One default network; no external services.

## Data directory mapping
- Update repository to write JSON files under `DATA_DIR` (default `/app/data`).
- In Compose, mount a named volume at `/app/data` for persistence.

## Commands (to include in README or docs)
- Build: `docker compose build`.
- Run: `docker compose up -d`.
- Logs: `docker compose logs -f app`.
- Stop: `docker compose down`.

## Notes / decisions resolved
- Runtime base image: `aspnet:10.0-alpine`.
- Data persistence: write JSON under `/app/data` with a named volume mount.
