# Choose My Sprint Name

A small webapp for picking a sprint name by voting on Pokemon that start with a chosen letter. See [prd/sprint-name-voter.md](prd/sprint-name-voter.md).

## Stack

- **Backend**: ASP.NET Core 9 (C#)
- **Frontend**: Vue 3 + Vite
- **Database**: SQLite (winners history only)
- **Hosting**: Render (single Docker service)

## Repository layout

```
backend/          ASP.NET Core minimal API; serves the built SPA + /api/*
backend.Tests/    xUnit tests
frontend/         Vue 3 + Vite SPA
prd/              Product requirements
issues/           Vertical-slice implementation issues
Dockerfile        Multi-stage build for Render
```

## Local development

Run backend and frontend separately (fast feedback loop):

```sh
# Terminal 1 - backend on http://localhost:5000
dotnet run --project backend

# Terminal 2 - frontend dev server (proxies /api to backend)
cd frontend
npm install
npm run dev
```

Or build the production bundle and serve it from the backend:

```sh
cd frontend && npm install && npm run build
# copy the build into the backend's wwwroot
# (the Dockerfile does this automatically for prod)
```

## Tests

```sh
dotnet test
```

## Deploy to Render

The included `Dockerfile` is a self-contained multi-stage build (Node -> .NET SDK -> ASP.NET runtime).

1. Push the repo to GitHub.
2. In the Render dashboard, create a new **Web Service** from the repo.
3. Choose the **Docker** runtime; Render detects the `Dockerfile` automatically.
4. The container listens on port `8080`; Render maps it to the public URL.
5. No environment variables required for the initial deploy.

### Persistence note

The winners history is stored in a SQLite file (`winners.db`, location configurable via `Sqlite:WinnersDbPath`). On Render this file lives on the container's local ephemeral filesystem and is **reset on every redeploy or restart**. To persist winners across deploys, attach a [Render Disk](https://render.com/docs/disks) and point `Sqlite:WinnersDbPath` (or env var `Sqlite__WinnersDbPath`) at a path inside the mounted disk. Disk attachment is out of scope for the initial setup.
