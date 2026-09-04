# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

The admin console for `../Backend` — a Blazor Web App where a site admin manages
their sites and moderates comments. It is an **HTTP client of the API**, not a
co-deployed frontend: it owns no database, no entities, and no authorization
rules. Part of `Komment.slnx`; see the root `CLAUDE.md` for the solution and
`../Backend/CLAUDE.md` before touching the API it calls.

## Commands

```bash
dotnet run                    # https://localhost:7222 — needs Backend already up
dotnet run --project ../AppHost   # from repo root: Backend + Dashboard + tailwind watch
dotnet build                  # runs `npm ci` then `npm run build` first (see below)
npm run dev                   # vite build --watch, standalone; AppHost runs this for you
```

There is no test project. Running standalone works because
`appsettings.Development.json` hard-codes `Services:backend` to
`https://localhost:7017`; under AppHost, `.WithReference(backend)` supplies the
same env vars and wins.

CSS is an MSBuild concern: `Dashboard.csproj` has a `ViteBuild` target hooked
`BeforeTargets="ResolveStaticWebAssetsInputs"`, so `wwwroot/dist` exists before
Blazor collects static assets. Never commit-fix CSS by editing `wwwroot/dist`.

## Architecture

**Every page is static SSR. Nothing declares a render mode.** Interactive server
components are registered in `Program.cs` and never used — the root `CLAUDE.md`
line about "per-page render modes" describes the template, not this code. So:

- Mutations are real form POSTs (`EditForm` + `[SupplyParameterFromForm]`), each
  with a unique `FormName` — hence `FormName="@($"delete-{site.SiteId}")"` inside
  loops. Two forms sharing a name silently break.
- A POST re-runs `OnInitializedAsync` before the handler. Pages guard against
  clobbering typed input (`if (Input is not null) return;` in `SiteEditor`,
  `Input ??= new()` in `CommentEditor`) and rely on that re-run to repopulate
  fields the handler needs (`siteSlug`, `target`).
- No `@onclick`, no JS interop for state. Confirmations are native `confirm()` on
  the submit button; the sidebar toggle is `peer-checked:`.
- Adding `@rendermode InteractiveServer` to a page breaks its forms and, on
  `Login`/`Register`, makes writing the auth cookie impossible.

**`ApiComponent` is the base class for anything that calls the API.** Inherit it
(`@inherits ApiComponent`) rather than injecting `IHttpClientFactory`: it gives
`Api` (the correctly-configured client), the shared `error` field pages render as
a red banner, `GuardAsync` (turns an unreachable API into a sentence, not a stack
trace), `FirstErrorAsync` (unwraps FastEndpoints' `{ errors: { field: [msg] } }`),
and `CurrentUserIdAsync`.

**Two cookies, one identity.** The Dashboard's own auth cookie carries the API's
`comments.session` value as a claim (`BackendSessionHandler.SessionClaim`);
`BackendSessionHandler` replays it per request so each user's calls carry that
user's API session. Three things hold this together — break any one and sessions
leak between users:

1. The named client (`BackendSessionHandler.ClientName`) sets
   `UseCookies = false` on its primary handler. `IHttpClientFactory` pools one
   primary handler across all callers, so a shared `CookieContainer` would replay
   the last login for everyone.
2. `Login.razor` reads `Set-Cookie` off the login response and stores it as a
   claim. Its follow-up `/api/auth/me` call attaches the cookie *by hand* — the
   handler reads `HttpContext.User`, which is still anonymous mid-login.
3. Never `new HttpClient()`. Anything talking to the API goes through the named
   client, which is what service discovery resolves `https+http://backend` on.

**The API is the trust boundary; this app only reports its rules.** Reader-vs-
admin (`Login` rejects a non-`IsSiteAdmin` account after a successful password
check), the `MULTI_TENANCY` registration gate (`Register` reads a 403 as "closed"),
per-owner scoping — all decided by `Backend`. DataAnnotations on the `Input`
classes mirror the API's validators for convenience only.

**Owner scoping is done by fetching the site.** `/api/site/{id}` is owner-scoped,
so `Comments` and `CommentEditor` fetch it first and treat a non-success as
"does not exist". That fetch is both the authorization check and the source of
the slug — comment endpoints key off `site=<slug>`, and `CommentResponse` carries
no site of its own, so the site id lives in the route.

**Routes.** `/sites` (list) → `/sites/new` | `/sites/{id}` (`SiteEditor`, one
component for both) → `/sites/{id}/comments` → `/sites/{id}/comments/{id}`, where
`?reply=true` switches `CommentEditor` between editing and replying.

## Conventions

- Response DTOs are `sealed record`s at the project root mirroring the API's
  shapes (`SiteResponse`, `CommentResponse`). Add one only when a page needs it;
  requests go out as anonymous objects.
- Tailwind utilities in markup. `Styles/app.css` holds only what Blazor's own
  class names force into CSS (`.validation-message`, `.blazor-error-boundary`).
  Vite entry is `Styles/main.js` → `wwwroot/dist` with stable filenames; Blazor's
  `MapStaticAssets`/`@Assets[...]` does the fingerprinting.
- `BareLayout` for unauthenticated full-screen pages (`Login`, `Register`),
  `MainLayout` for the signed-in shell. `ReconnectModal` is the only scoped
  stylesheet in the project.
- API failures surface as a message in `error` (or a `notFound`/`blocked` flag),
  never as an exception page. `404` on a delete is treated as success.
- `ponytail:` comments mark deliberate shortcuts with their upgrade path.
  Respect them; do not "fix" them without a measured reason.
