# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A .NET 10 learning solution (`Komment.slnx`) for a developer coming from 9
years of Laravel. Three projects, orchestrated by Aspire:

| Project | What it is |
|---|---|
| `Backend/` | The real app — a self-hostable comment backend for static blogs. **Has its own `CLAUDE.md`; read it before touching anything in there.** |
| `Dashboard/` | Blazor Server (interactive server render mode), still the stock template. Does not call `Backend` yet. |
| `AppHost/` | Aspire 13 orchestrator (`Aspire.AppHost.Sdk/13.5.3`). Single-file `AppHost.cs`, no ServiceDefaults project. |

`LearningJourney/aspnet-roadmap.md` is a gitignored personal notes file with
Laravel→.NET translation tables. It is context for *why* the code looks like it
does; it is not documentation of this code.

## Commands

```bash
dotnet run --project AppHost          # everything + Aspire dashboard (no `aspire` CLI installed)
dotnet run --project Backend         # API alone: https://localhost:7017
dotnet run --project Dashboard        # frontend alone: https://localhost:7222
dotnet build                          # whole solution
```

EF Core migrations are Backend-only and must run from that directory
(`cd Backend && dotnet ef migrations add <Name>`). There is no test project.

## Solution-level notes

**Aspire wires the two apps but they are not yet connected.** `AppHost.cs`
`.WithReference(backend)` only injects `services__backend__*` env vars —
Dashboard has no `Microsoft.Extensions.ServiceDiscovery` package, so it cannot
resolve `https+http://backend`. Add that package before writing frontend code
that calls the API. This is marked with a `ponytail:` comment in `AppHost.cs`.

**No ServiceDefaults project.** The usual Aspire `AddServiceDefaults()`
(OpenTelemetry, health checks, resilient HTTP) is deliberately absent. Adding it
means creating the project and calling it from both apps' `Program.cs`.

**Backend loads `Backend/.env` relative to the working directory.** It works
under both `dotnet run --project Backend` and Aspire because each launches with
the project directory as CWD. Real environment variables win over `.env`.

**`ponytail:` comments mark deliberate shortcuts with their upgrade path.**
Respect them; do not "fix" them without a measured reason.
