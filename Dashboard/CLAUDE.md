# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

The admin console for `../Backend` — where a site admin manages their sites and
moderates comments. It is a **browser client of the API**, the same as a blog
is: it owns no database, no entities, no authorization rules, and no session.
Part of `Komment.slnx`; see the root `CLAUDE.md` for the solution and
`../Backend/CLAUDE.md` before touching the API it calls.

**The server renders HTML; Alpine does everything else.** Blazor matches the
route and emits a static shell. Every fetch, every render of API data, and every
decision about who is signed in happens in Alpine, **in the same `.razor` file as
the markup it drives**. The Dashboard server never calls the API and cannot tell
one visitor from another.

`Styles/main.js` is the only `.js` file in the project and is eight lines long:
it imports the stylesheet, imports Alpine, and starts it. Resist putting anything
else there — a page's behaviour belongs next to its markup.

## Commands

```bash
dotnet run                    # https://localhost:7222 — needs Backend already up
dotnet run --project ../AppHost   # from repo root: Backend + Dashboard + tailwind watch
dotnet build                  # runs `npm ci` then `npm run build` first (see below)
npm run build                 # bundle Styles/ into wwwroot/dist
npm run dev                   # vite build --watch, standalone; AppHost runs this for you
```

There is no test project, and nothing type-checks or lints the inline scripts —
read them carefully. `npm run build` will not catch an error inside a `<script>`
in a `.razor` file; only the browser will.

Running standalone works because `appsettings.Development.json` points
`Services:backend` at `https://localhost:7017`; under AppHost, `.WithReference`
supplies the same values and wins. Either way `ApiBaseUrl` reduces them to one
URL and `App.razor` writes it into `<meta name="komment-api">` — **that meta tag
is how the browser learns where the API is.**

CSS and the Alpine bundle are an MSBuild concern: `Dashboard.csproj` has a
`ViteBuild` target hooked `BeforeTargets="ResolveStaticWebAssetsInputs"`, so
`wwwroot/dist` exists before Blazor collects static assets. Never fix anything by
editing `wwwroot/dist`.

## Architecture

**Alpine, written inline.** Each page is markup plus one `<script>` at the bottom
of the same file registering its component:

```razor
<div x-data="sites" x-cloak> … </div>

<script>
    document.addEventListener('alpine:init', () => {
        Alpine.data('sites', () => ({ … }));
    });
</script>
```

`alpine:init` rather than a bare call, because `main.js` is a deferred module: the
inline script parses first and registers a listener, then Alpine starts and fires
it. Shared plumbing lives the same way in `App.razor` — an `$store.auth` store and
an `$api` magic, available to every page as `this.$store` / `this.$api`.

**Razor claims `@`, so Alpine's `@click` shorthand is spelled `x-on:click`.**
Everywhere, without exception. `:` bindings (`:href`, `:class`, `:disabled`) are
unaffected and used normally. A stray `@` inside a `<script>` in a `.razor` file
is a Razor transition and will not compile.

**Route parameters are arguments, not lookups.** The server's only contribution
to a page is the value in the route: `x-data="siteEditor('@Id')"`. Nothing else
crosses from server to browser.

**`x-text` escapes; that is the point.** Comment bodies are written by strangers
and the auth token is reachable from script. Bind text, never assemble markup.

**Pages are static SSR shells and there is no `blazor.web.js`.** Dropping the
framework script is what makes inline `<script>` viable: enhanced navigation
swaps the DOM without re-running it, so every `Alpine.data` registration would
fire once and then never again. Plain navigation keeps every page load honest.

**`x-cloak` on every component root.** Alpine binds after the deferred module
runs, so anything driven by `x-show` or `x-text` is briefly present and wrong
without it. The rule lives in `app.css`.

**Auth is a bearer token in `localStorage`, held by the `$store.auth` store.**
`POST /api/auth/token` returns it with the caller's identity, so there is no
follow-up `/api/auth/me`. Every page behind the sign-in opens `init()` with
`if (!this.$store.auth.requireAdmin()) return;` — it redirects to
`/login?returnUrl=…` and returns false when the token is missing, expired, or
belongs to a reader. Signing out is client-side only: the API has no revocation
list, and `Tokens.Lifetime` is the real expiry.

**The API is the trust boundary; this app only reports its rules.** Reader-vs-
admin, the `MULTI_TENANCY` registration gate (a 403 on register means "closed"),
per-owner scoping — all decided by `Backend`. There are no DataAnnotations
mirrors of the API's validators any more: forms carry `required`/`maxlength` as
the browser's own cheap first pass, and everything else comes back as an error
from the API.

**Owner scoping is done by fetching the site.** `/api/site/{id}` is owner-scoped,
so `Comments` and `CommentEditor` fetch it first and treat a non-success as
"does not exist" (`$api.offline(failure)` separates that from an unreachable API). That fetch is both the authorization check and the source
of the slug — comment endpoints key off `site=<slug>`, and a comment response
carries no site of its own.

**Routes.** `/sites` → `/sites/new` | `/sites/{id}` (`SiteEditor`, one shell for
both, distinguished by whether the component was given an id) →
`/sites/{id}/comments` → `/sites/{id}/comments/{id}`, where `?reply=true` switches
`CommentEditor` between editing and replying.

## Conventions

- Response shapes are not modelled anywhere. The API's JSON is camelCase and
  Alpine reads it directly; there is no DTO layer to keep in sync.
- Tailwind utilities in markup, including inside `:class` object syntax — it all
  lives in `.razor` files, which is what `app.css` scans. `app.css` itself holds
  only the `h1:focus` rule `<FocusOnNavigate>` forces and the `[x-cloak]` rule.
- Vite entry is `Styles/main.js` → `wwwroot/dist` with stable filenames; Blazor's
  `MapStaticAssets`/`@Assets[...]` does the fingerprinting. Imports are static so
  the bundle stays one file under one name.
- Prefer CSS-only interactions to JS for presentation (the sidebar toggle is
  still `peer-checked:`). JS is for data, not for layout.
- `BareLayout` for unauthenticated full-screen pages (`Login`, `Register`),
  `MainLayout` for the signed-in shell. There are no scoped stylesheets.
- API failures surface as an `error` (or `blocked`/`notFound`) property rendered
  into a banner, never as an exception. `404` on a delete is treated as success.
- Buttons that submit carry `:disabled="busy"` and an `x-text` that names what is
  happening, so a slow API cannot be double-submitted.
- `ponytail:` comments mark deliberate shortcuts with their upgrade path.
  Respect them; do not "fix" them without a measured reason.
