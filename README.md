# Dinner

AI-assisted, low-effort meal planning with recipe management, pantry tracking, nutrition estimates and automated shopping lists.

## Project layout

- `src/services/`: ASP.NET Core service hosts.
- `src/services/<service>/Dockerfile`: service-specific container build definitions.
- `src/Dinner.ServiceDefaults/`: shared correlation, error, logging, and health conventions.
- `src/web/`: Next.js application shell.
- `contracts/`: initial API, event, MCP, and agent-webhook conventions.

## Prerequisites

- .NET SDK 10.0.403 (pinned in `global.json`)
- Node.js 24 and npm 11
- Docker and Docker Compose v2

## Run locally

Start the local stack:

```bash
./run.sh
```

This starts PostgreSQL, the seven backend services, and the web shell as separate containers. The web shell is available on `http://localhost:${DINNER_WEB_PORT:-3000}`. Backend health endpoints are exposed on the service ports defined in `.env.example`.

The first run applies the household and pantry schema migrations and creates a local sample household with a tinned-tomatoes pantry item. The Pantry screen can add items, consume or restore stock, and remove items. Supported input units are `g`, `kg`, `ml`, `l`, and `item`; quantities are stored as grams, millilitres, or items.

| Component | Local address |
| --- | --- |
| Agent Gateway | `http://localhost:18081/health` |
| Household Service | `http://localhost:18082/health` |
| Pantry Service | `http://localhost:18083/health` |
| Recipe Service | `http://localhost:18084/health` |
| Nutrition Data Service | `http://localhost:18085/health` |
| Meal Planning Service | `http://localhost:18086/health` |
| Shopping List Service | `http://localhost:18087/health` |

Build all backend service hosts:

```bash
for project in src/services/*/*.csproj; do
  dotnet build "$project"
done
```

`Dinner.sln` contains the same projects for IDE use.

Run an individual service (the health endpoint is available at `/health`):

```bash
dotnet run --project src/services/Dinner.HouseholdService
```

Start the web shell:

```bash
cd src/web
npm install
npm run dev
```

## Local PostgreSQL

The project includes a Docker Compose PostgreSQL service. Docker and Docker Compose v2 are required.

```bash
./run.sh
```

On first run, `run.sh` creates `.env` from `.env.example`. Set a strong `POSTGRES_PASSWORD` in `.env` before using this outside local development. PostgreSQL is exposed on `localhost:${POSTGRES_PORT}` and its data is stored in the `postgres_data` Docker volume.

To stop the service:

```bash
docker compose --env-file .env down
```
