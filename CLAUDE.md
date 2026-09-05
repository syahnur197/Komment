# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A .NET 10 solution (`Komment.slnx`). Three projects, orchestrated by Aspire:

| Project | What it is |
|---|---|
| `Backend/` | The real app — a self-hostable comment backend for static blogs. **Has its own `CLAUDE.md`; read it before touching anything in there.** |
| `Dashboard/` | The admin console for `Backend`. Blazor renders static HTML shells; the browser calls the API. Tailwind v4 + browser JS via Vite. |
| `AppHost/` | Aspire 13 orchestrator (`Aspire.AppHost.Sdk/13.5.3`). Single-file `AppHost.cs`, no ServiceDefaults project. |

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

Postgres comes from whichever host is running: `AddPostgres("postgres")` in
AppHost starts a container and injects `ConnectionStrings__comments`; the compose
files build the same connection string from `POSTGRES_PASSWORD`. Backend reads it
with plain `GetConnectionString("comments")` and no Aspire client integration, so
`dotnet run --project Backend` alone falls back to the localhost default in
`appsettings.json`.

CSS **and the Dashboard's JavaScript** are built by MSBuild — `Dashboard.csproj`
runs `npm ci` then `npm run build` before static assets are collected, so
`dotnet build` alone is enough. Under AppHost, `npm run dev` (a
`vite build --watch`, not a dev server) runs as a plain executable resource; a
browser refresh picks up a rebuild.

The API needs `JWT_SIGNING_KEY` outside Development and `DASHBOARD_ORIGIN`
always; `docker-compose.prod.yml` also needs `BACKEND_PUBLIC_URL`. See
`.env.example`.

## Solution-level notes

**The Dashboard is a browser client of the API, exactly like a blog is.** Its
server renders static HTML shells and nothing else — no `HttpClient`, no session,
no idea who is signed in. Everything that touches the API lives in
`Dashboard/Styles/js/`, and the admin's credential is a bearer token in their own
browser's `localStorage`. The one thing the server still tells the browser is
where the API is: `ApiBaseUrl` resolves it and `App.razor` writes it into
`<meta name="komment-api">`.

That means **the API must let the console's origin through CORS** — it has no row
in the `Sites` table, so it comes from `DASHBOARD_ORIGIN` — and that a blog and
the console reach the API by different credentials: cookie for readers, bearer
for admins. `Backend`'s `"smart"` policy scheme picks per request.

**Dashboard pages are static SSR shells, and there is no `blazor.web.js`.**
Nothing is interactive; the framework script would only add enhanced navigation,
which swaps the DOM without re-running the page modules. Identity-dependent
markup starts `hidden` and is revealed by JS. Never introduce `innerHTML` there —
see `Dashboard/CLAUDE.md`.

**The API owns every authorization rule; the Dashboard only reports them.**
Reader-vs-admin (`IsSiteAdmin`), the `MULTI_TENANCY` registration gate,
per-owner site scoping — all decided by `Backend`. The Dashboard keeps no mirror
of the API's validators; it surfaces API failures as messages rather than stack
traces.

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
`X-Forwarded-Proto`. Postgres runs as a third service with its own volume,
Backend migrates on boot behind a `service_healthy` gate, and both apps persist
`DATAPROTECTION_KEYS` on the **Backend** so a restart does not sign every blog
reader out — the Dashboard no longer needs it, having no cookie of its own.
Admin tokens survive restarts as long as `JWT_SIGNING_KEY` is stable.
`BACKEND_PUBLIC_URL` must be the tunnel's public API hostname, because the
browser is what resolves it. The plain `docker-compose.yml` is the local-only
version: no tunnel, so the `SameSite=None; Secure` reader cookie will not stick
in a browser — the admin console still works, since a bearer token does not
care.

**`ponytail:` comments mark deliberate shortcuts with their upgrade path.**
Respect them; do not "fix" them without a measured reason.

## Dashboard conventions

- Tailwind utilities in markup *and* in the JS that builds rows — `app.css`
  scans `Styles/js/**/*.js` too, or those classes get purged. `app.css` itself
  holds only the `h1:focus` rule `<FocusOnNavigate>` forces.
  Vite entry is `Styles/main.js`, output `wwwroot/dist` with stable filenames —
  Blazor's `MapStaticAssets`/`@Assets[...]` does the fingerprinting.
- Prefer CSS-only interactions to presentational JS (the sidebar toggle is
  `peer-checked:`). There are no scoped stylesheets.
- `BareLayout` for unauthenticated full-screen pages, `MainLayout` for the
  signed-in shell.
