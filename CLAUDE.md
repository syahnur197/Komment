# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A .NET 10 solution (`Komment.slnx`). Two projects, orchestrated by Aspire:

| Project | What it is |
|---|---|
| `Backend/` | The whole app — a self-hostable comment backend for static blogs, plus the admin console that manages it. **Has its own `CLAUDE.md`; read it before touching anything in there.** |
| `AppHost/` | Aspire 13 orchestrator (`Aspire.AppHost.Sdk/13.5.3`). Single-file `AppHost.cs`, no ServiceDefaults project. |

`Backend/` is one ASP.NET process serving two audiences. `/api/*` is
FastEndpoints, called cross-origin by blogs. Everything else is Blazor: the
admin console, rendered interactively on the server. They share a database, a
service layer and a process — but not a cookie.

## Commands

```bash
dotnet run --project AppHost          # app + Postgres + Aspire dashboard (no `aspire` CLI installed)
dotnet run --project Backend          # app alone: https://localhost:7017 (needs a local Postgres)
dotnet build                          # whole solution; also runs npm ci + vite build
docker compose up --build             # one image on :8017
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

CSS is built by MSBuild — `Backend.csproj` runs `npm ci` then `npm run build`
before static assets are collected, so `dotnet build` alone is enough. Node is
only there for Tailwind; the console ships no application JavaScript. Under
AppHost, `npm run dev` (a `vite build --watch`, not a dev server) runs as a plain
executable resource; a browser refresh picks up a rebuild.

See `.env.example` for the environment it reads.

## Solution-level notes

**One process, two audiences, two cookies.** A blog is a static site on another
origin, so its readers carry `comments.session` — `SameSite=None; Secure`, which
is what a third-party cookie has to be. An admin is on this origin, so the console
carries `komment.admin` — `SameSite=Lax`, which also means it still works over
plain HTTP under `docker compose up`. A path policy scheme in `Program.cs`
forwards `/api/*` to the reader cookie and everything else to the admin cookie,
so neither handler ever sees the other's. `AuthSchemes` names all three.

**CORS is for blogs only.** `SiteOrigins.IsAllowed` reads the `Sites` table per
preflight, so registering a blog is an "Add site" in the console (or a
`POST /api/site`), not a redeploy. The console itself needs no entry — it is
served from this origin.

**The service layer owns every authorization rule.** `SiteService`,
`CommentService` and `AccountService` decide reader-vs-admin, per-owner site
scoping, author-only edits, author-or-owner deletes and the `MULTI_TENANCY`
gate. Endpoints and components both go through them and neither touches
`AppDbContext`. This used to be enforced by the HTTP boundary between two apps;
in one project it is a convention, so it matters more, not less.

**Console pages are `@rendermode InteractiveServer`, except the auth pages.**
Components run in this process and call the services directly — no HTTP, no
DTOs, no client-side JavaScript. `Login.razor` / `Register.razor` declare no
render mode on purpose: a cookie can only be written during a real form POST,
which an interactive circuit does not give you. Do not add a render mode to
them, and do not remove it from the others.

**No ServiceDefaults project.** The usual Aspire `AddServiceDefaults()`
(OpenTelemetry, health checks, resilient HTTP) is deliberately absent. Adding it
means creating the project and calling it from both apps' `Program.cs`.

**Backend loads the solution-root `.env` relative to the working directory.**
It looks for `.env` then `../.env`, so it works under `dotnet run --project
Backend` and AppHost (project directory as CWD) and in Docker, where the file is
absent and `docker-compose.yml` passes the same keys as real environment
variables. Real environment variables win over `.env` either way.

**Production is `docker-compose.prod.yml`, fronted by a Cloudflare Tunnel that
lives on the server, not in this repo.** The app publishes plain HTTP to
`127.0.0.1:8017` and the host's `cloudflared` maps one public hostname onto it —
`/api/*` and the console are the same origin now. It runs with
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` because the tunnel is a proxy: the
Google `redirect_uri` and the `Secure` cookie both depend on
`X-Forwarded-Proto`. **The tunnel must pass WebSockets** — the console is
interactive server rendering, so it needs a circuit. Postgres runs alongside
with its own volume, the app migrates on boot behind a `service_healthy` gate,
and `DATAPROTECTION_KEYS` persists to a volume: both cookies are encrypted with
that keyring, so losing it signs out every reader and every admin at once. The
plain `docker-compose.yml` is the local-only version: no tunnel, so the
`SameSite=None; Secure` reader cookie will not stick in a browser — the console
still works, because its cookie is `Lax`.

**`ponytail:` comments mark deliberate shortcuts with their upgrade path.**
Respect them; do not "fix" them without a measured reason.

## Console conventions

- Tailwind utilities in markup. `Styles/app.css` holds only what Blazor's own
  class names (`.invalid`, `.validation-message`, `.blazor-error-boundary`)
  force into CSS, plus the `h1:focus` rule `<FocusOnNavigate>` needs.
  Vite entry is `Styles/main.js` — CSS only, there is no application JavaScript.
  Output `wwwroot/dist` with stable filenames; Blazor's
  `MapStaticAssets`/`@Assets[...]` does the fingerprinting.
- Prefer CSS-only interactions where they work (the sidebar toggle is
  `peer-checked:`). Destructive actions confirm inline with component state
  rather than a JS `confirm()` — interactivity makes the safer version simpler.
- `BareLayout` for unauthenticated full-screen pages, `MainLayout` for the
  signed-in shell. `ReconnectModal` is the only scoped stylesheet.
