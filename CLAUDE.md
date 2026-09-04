# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A .NET 10 learning solution (`Komment.slnx`) for a developer coming from 9
years of Laravel. Three projects, orchestrated by Aspire:

| Project | What it is |
|---|---|
| `Backend/` | The real app — a self-hostable comment backend for static blogs. **Has its own `CLAUDE.md`; read it before touching anything in there.** |
| `Dashboard/` | Blazor Web App (per-page render modes) — the admin console for `Backend`. Tailwind v4 via Vite. |
| `AppHost/` | Aspire 13 orchestrator (`Aspire.AppHost.Sdk/13.5.3`). Single-file `AppHost.cs`, no ServiceDefaults project. |

`LearningJourney/aspnet-roadmap.md` is a gitignored personal notes file with
Laravel→.NET translation tables. It is context for *why* the code looks like it
does; it is not documentation of this code.

## Commands

```bash
dotnet run --project AppHost          # everything + Aspire dashboard (no `aspire` CLI installed)
dotnet run --project Backend          # API alone: https://localhost:7017
dotnet run --project Dashboard        # frontend alone: https://localhost:7222 (needs Backend up)
dotnet build                          # whole solution; also runs npm ci + vite build
docker compose up --build             # both images; backend :8017, dashboard :8222
docker compose -f docker-compose.prod.yml up -d --build   # behind the host's tunnel
```

EF Core migrations are Backend-only and must run from that directory
(`cd Backend && dotnet ef migrations add <Name>`). There is no test project.

CSS is built by MSBuild — `Dashboard.csproj` runs `npm ci` then `npm run build`
before static assets are collected, so `dotnet build` alone is enough. Under
AppHost, `npm run dev` (a `vite build --watch`, not a dev server) runs as a
plain executable resource; a browser refresh picks up a rebuild.

## Solution-level notes

**The Dashboard is an HTTP client of the API, not a co-deployed frontend.** It
holds *two* cookies' worth of identity: its own ASP.NET auth cookie, and — as a
claim inside it — the API's `comments.session` cookie captured from the login
response. `BackendSessionHandler` replays that claim on every outbound call, so
each user's API requests carry that user's session. Anything that talks to the
API must go through the named `HttpClient` (`BackendSessionHandler.ClientName`),
never a bare `new HttpClient()`. Service discovery resolves
`https+http://backend` from the env vars Aspire's `.WithReference` injects.

**Auth pages are static SSR on purpose.** `Login.razor` / `Register.razor`
declare no render mode: a cookie can only be written during the real form POST,
which interactive server rendering does not give you. Do not add
`@rendermode InteractiveServer` to them.

**The API owns every authorization rule; the Dashboard only reports them.**
Reader-vs-admin (`IsSiteAdmin`), the `MULTI_TENANCY` registration gate,
per-owner site scoping — all decided by `Backend`. Dashboard code mirrors
validation for convenience but treats the API as the trust boundary, and
surfaces API failures as messages rather than stack traces.

**No ServiceDefaults project.** The usual Aspire `AddServiceDefaults()`
(OpenTelemetry, health checks, resilient HTTP) is deliberately absent. Adding it
means creating the project and calling it from both apps' `Program.cs`.

**Backend loads the solution-root `.env` relative to the working directory.**
It looks for `.env` then `../.env`, so it works under `dotnet run --project
Backend` and AppHost (project directory as CWD) and in Docker, where the file is
absent and `docker-compose.yml` passes the same keys as real environment
variables. Real environment variables win over `.env` either way.

**Production is `docker-compose.prod.yml`, fronted by a Cloudflare Tunnel that
lives on the server, not in this repo.** Both apps publish plain HTTP to
`127.0.0.1` (backend `:8017`, dashboard `:8222`) and the host's `cloudflared`
maps public hostnames onto those ports. Both run with
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` because the tunnel is a proxy — the
Google `redirect_uri` and the `Secure` cookie both depend on
`X-Forwarded-Proto`. Backend migrates its SQLite file on boot, and both apps
persist data-protection keys to a volume when `DATAPROTECTION_KEYS` is set,
without which a restart invalidates every cookie. The plain `docker-compose.yml`
is the local-only version: no tunnel, so the `SameSite=None; Secure` session
cookie will not stick in a browser.

**`ponytail:` comments mark deliberate shortcuts with their upgrade path.**
Respect them; do not "fix" them without a measured reason.

## Dashboard conventions

- Tailwind utilities in markup; `Styles/app.css` holds only what Blazor's own
  class names (`.validation-message`, `#blazor-error-ui`) force into CSS.
  Vite entry is `Styles/main.js`, output `wwwroot/dist` with stable filenames —
  Blazor's `MapStaticAssets`/`@Assets[...]` does the fingerprinting.
- Prefer CSS-only interactions to JS (the sidebar toggle is `peer-checked:`).
  There are no `.razor.css` scoped stylesheets outside `ReconnectModal`.
- `BareLayout` for unauthenticated full-screen pages, `MainLayout` for the
  signed-in shell.
