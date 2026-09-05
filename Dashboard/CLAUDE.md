# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

The admin console for `../Backend` — where a site admin manages their sites and
moderates comments. It is a **browser client of the API**, the same as a blog
is: it owns no database, no entities, no authorization rules, and no session.
Part of `Komment.slnx`; see the root `CLAUDE.md` for the solution and
`../Backend/CLAUDE.md` before touching the API it calls.

**The server renders HTML; the browser does everything else.** Blazor matches
the route and emits a static shell. Every fetch, every render of API data, and
every decision about who is signed in happens in `Styles/js/`. The Dashboard
server never calls the API and cannot tell one visitor from another.

## Commands

```bash
dotnet run                    # https://localhost:7222 — needs Backend already up
dotnet run --project ../AppHost   # from repo root: Backend + Dashboard + tailwind watch
dotnet build                  # runs `npm ci` then `npm run build` first (see below)
npm run build                 # bundle Styles/ into wwwroot/dist
npm run dev                   # vite build --watch, standalone; AppHost runs this for you
```

There is no test project. `node --check Styles/js/**/*.js` is the only syntax
gate on the browser code, so read it carefully.

Running standalone works because `appsettings.Development.json` points
`Services:backend` at `https://localhost:7017`; under AppHost, `.WithReference`
supplies the same values and wins. Either way `ApiBaseUrl` reduces them to one
URL and `App.razor` writes it into `<meta name="komment-api">` — **that meta tag
is how the browser learns where the API is.**

CSS *and JS* are an MSBuild concern: `Dashboard.csproj` has a `ViteBuild` target
hooked `BeforeTargets="ResolveStaticWebAssetsInputs"`, so `wwwroot/dist` exists
before Blazor collects static assets. Never fix anything by editing
`wwwroot/dist`.

## Architecture

**`Styles/js/` is the application.** The Razor files are shells.

| File | Job |
|---|---|
| `api.js` | The only place that calls `fetch`. Base URL, `Authorization` header, 401 handling, FastEndpoints error parsing. |
| `auth.js` | The token in `localStorage`, and `requireAdmin()` — the guard every signed-in page opens with. |
| `dom.js` | Element building, banners, dates. |
| `router.js` | Reads `data-page` off the shell and runs the matching module. |
| `pages/*.js` | One per route. |
| `layout.js` | Reveals the parts of the shell that depend on being signed in. |

**Never use `innerHTML`, and never build markup by concatenating strings.**
Razor escaped interpolated values; `dom.js` does not exist to be convenient, it
exists because comment bodies are written by strangers and the auth token is now
reachable from script. Text goes in as a text node — `h()` and `textContent`, always.

**Pages are static SSR shells. Nothing declares a render mode, and there is no
`blazor.web.js`.** Dropping the framework script is deliberate: nothing is
interactive, and its enhanced navigation swaps the DOM without re-running the
page modules, so every link would work once and then stop. Consequences:

- A shell renders the same HTML for every visitor. Anything identity-dependent
  starts `hidden` and is revealed by JS — see `layout.js` and `[data-content]`.
- The server passes route parameters to the browser through `data-` attributes
  (`data-site-id`), never by fetching anything itself.
- Forms are plain `<form novalidate>` with `name` attributes, submitted by
  `preventDefault` + `fetch`. No `EditForm`, no `[SupplyParameterFromForm]`.
- Tailwind must be told to scan `Styles/js/**/*.js` (it is, in `app.css`) or
  every class used only from JS is purged.

**Auth is a bearer token in `localStorage`.** `POST /api/auth/token` returns it
along with the caller's identity, so there is no follow-up `/api/auth/me`.
`requireAdmin()` redirects to `/login?returnUrl=…` when it is missing, expired,
or belongs to a reader rather than an admin; a page that gets `null` back must
render nothing. Signing out is client-side only — the API has no revocation
list, and `Tokens.Lifetime` is the real expiry.

**The API is the trust boundary; this app only reports its rules.** Reader-vs-
admin, the `MULTI_TENANCY` registration gate (a 403 on register means "closed"),
per-owner scoping — all decided by `Backend`. There are no DataAnnotations
mirrors of the API's validators any more: forms carry `required`/`maxlength` as
the browser's own cheap first pass, and everything else comes back as an error
from the API.

**Owner scoping is done by fetching the site.** `/api/site/{id}` is owner-scoped,
so `comments.js` and `comment-editor.js` fetch it first and treat a non-success
as "does not exist". That fetch is both the authorization check and the source
of the slug — comment endpoints key off `site=<slug>`, and a comment response
carries no site of its own.

**Routes.** `/sites` → `/sites/new` | `/sites/{id}` (`SiteEditor`, one shell for
both, distinguished by whether `data-site-id` is set) → `/sites/{id}/comments` →
`/sites/{id}/comments/{id}`, where `?reply=true` switches `comment-editor.js`
between editing and replying.

## Conventions

- Response shapes are not modelled anywhere. The API's JSON is camelCase and the
  JS reads it directly; there is no DTO layer to keep in sync.
- Tailwind utilities in markup and in `h()` calls. `Styles/app.css` holds only
  the `h1:focus` rule that `<FocusOnNavigate>` forces.
- Vite entry is `Styles/main.js` → `wwwroot/dist` with stable filenames; Blazor's
  `MapStaticAssets`/`@Assets[...]` does the fingerprinting. Imports are static so
  the bundle stays one file under one name.
- Prefer CSS-only interactions to JS for presentation (the sidebar toggle is
  still `peer-checked:`). JS is for data, not for layout.
- `BareLayout` for unauthenticated full-screen pages (`Login`, `Register`),
  `MainLayout` for the signed-in shell. There are no scoped stylesheets.
- API failures surface as a message in a `[data-error]` banner, never as an
  exception. `404` on a delete is treated as success.
- `ponytail:` comments mark deliberate shortcuts with their upgrade path.
  Respect them; do not "fix" them without a measured reason.
