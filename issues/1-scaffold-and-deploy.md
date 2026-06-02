# 1 - Scaffold C# backend serving Vue SPA + initial Render deploy

## Parent PRD

prd/sprint-name-voter.md

## What to build

A minimal single-service application: an ASP.NET Core backend that serves a built Vue 3 SPA as static assets, deployed to Render. The app should respond to `/` with a placeholder Vue page and expose a trivial `/api/health` endpoint returning OK. This is the tracer bullet that proves the deployment topology described in the PRD's "Architectural Decisions" section (single Render service).

## Acceptance criteria

- [ ] C# (ASP.NET Core) backend project created
- [ ] Vue 3 frontend project created (Vite)
- [ ] Production build of the Vue SPA is served as static files by the backend
- [ ] `/api/health` endpoint returns 200 OK
- [ ] Local `dotnet run` serves the SPA at the root URL
- [ ] App is deployed to Render as a single service and reachable via public URL
- [ ] README documents local dev and deploy steps

## Blocked by

None - can start immediately.

## User stories addressed

- User story 20
