# dinner
AI-assisted, low-effort meal planning with recipe management, pantry tracking, nutrition estimates and automated shopping lists.

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
