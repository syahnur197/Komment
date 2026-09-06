# AGENTS.md

This file provides guidance to Codex when working with code in `Backend/`.

## What this is

A self-hostable comment backend for static blogs, whatever generator built them,
and the admin console that manages it. One ASP.NET process, two audiences:

- `/api/*`: FastEndpoints. A blog is a static site on another origin, so this is
  served over CORS with a cross-site session cookie.
- Everything else: Blazor. The console is rendered interactively on the server.

They share `Data/`, `Services/` and a process. They do not share a cookie.

`../AppHost` is an Aspire host that starts Postgres and this app. Nothing here
depends on Aspire: no ServiceDefaults reference, no telemetry wiring. Use
`dotnet run` as the fast path if you have a local Postgres. Use
`dotnet run --project ../AppHost` when you want the container and the Aspire
dashboard.

## Commands

```bash
dotnet run                                  # https://localhost:7017 + http://localhost:5147; needs local Postgres
dotnet run --project ../AppHost             # same, but starts Postgres in a container
dotnet build                                # also runs npm ci + vite build for Tailwind
npm run build                               # Tailwind alone, into wwwroot/dist
dotnet ef migrations add <Name>             # after any entity/DbContext change
dotnet ef database update                   # or just start the app; it migrates on boot
```

Swagger UI is at `/swagger` in Development only, and documents `/api/*`. The
console is not an API and does not appear there. There is no test project yet.

## Architecture

**`Services/` owns every rule; `Features/` and `Components/` only phrase them.**
`SiteService`, `CommentService` and `AccountService` decide what is allowed:
owner-scoping, author-only edits, author-or-owner deletes, the `MULTI_TENANCY`
gate, and comment field rules that require database context. They answer with a
`Result` carrying `Ok`/`NotFound`/`Forbidden`/`Invalid`. An endpoint turns that
into a status code; a component turns it into a message. Nothing outside
`Services/` and `Data/` touches `AppDbContext`, except `SiteOrigins`, which is
CORS infrastructure, not a request handler.

**FastEndpoints (REPR), not controllers.** One file per endpoint under
`Features/<Area>/`, each holding its request DTO, its `Validator<TRequest>`, and
the `Endpoint<,>` class. `AddFastEndpoints()` scans the assembly at startup.
There is no route table, and validators are discovered by request type. Adding
an endpoint means adding a file, nothing else.

**Groups own the route prefix and the default auth.** `CommentGroup`
(`/api/comment`) is open for writes-behind-cookie with reads opting out via
`AllowAnonymous()`. `SiteGroup` (`/api/site`) requires the `site-admin` role on
every endpoint. Auth endpoints are ungrouped and declare full paths.

**Dependencies come in through the primary constructor**, kept in a
`private readonly` field named after the type:

```csharp
public sealed class GetAllSitesEndpoint(SiteService siteService) : EndpointWithoutRequest<List<SiteResponse>>
{
    private readonly SiteService _siteService = siteService;
```

FastEndpoints also supports property injection
(`public SiteService Sites { get; set; } = default!;`). Do not use it. Same names
in the console: `@inject SiteService _siteService`. Nothing injected is public,
and nothing is addressed by a nickname: the field is the type in camelCase with
a leading underscore, so the call site says which service it is.
`@inject NavigationManager Nav` is the one holdover. Framework-supplied component
members are private where Blazor allows it, such as `[CascadingParameter] private`
and `[SupplyParameterFromQuery] private`.

**Sites are tenants and also the CORS allowlist.** `SiteOrigins.IsAllowed` reads
the `Sites` table per preflight, so registering a blog is `POST /api/site` rather
than a redeploy. The same table backs `SafeReturnUrl`, which is the open-redirect
guard on the OAuth callback. The admin console is the one origin that is not a
row in that table, and needs none: it is served from this same origin.

**Two account kinds, one `Users` table.** Readers sign in with Google
(`GoogleId` set). Site admins register with username/password (`IsSiteAdmin`,
PBKDF2 via `PasswordHasher<User>`). Both paths build claims through
`UserClaims.For`/`UserClaims.UserId` so endpoints read the user id off the
principal and never re-look-up. `MULTI_TENANCY` decides whether registration
stays open or closes after the first admin.

**Two cookies, chosen by path.** `AuthSchemes.Reader` (`comments.session`,
`SameSite=None; Secure`) is the blog reader's cookie because the blog is on
another origin. `AuthSchemes.Admin` (`komment.admin`, `SameSite=Lax`) is the
console's cookie, and being `Lax` is why it still works over the plain HTTP that
`docker compose up` serves. A policy scheme forwards `/api/*` to the reader and
everything else to the admin. `DefaultSignInScheme` must name a real scheme: a
policy scheme cannot receive the Google handler's sign-in. `SiteGroup` names
`AuthSchemes.Admin` explicitly because it sits under `/api` but is an admin
operation. Inside an `Endpoint<>`, `AuthSchemes` is also a base-class method, so
the static class needs its full name when referenced from endpoints:
`Backend.Features.Auth.AuthSchemes.Reader`.

**Cookie config is deliberate and load-bearing.** `SameSite=None` + `Secure`
because the blog is cross-origin, and `OnRedirectToLogin`/`AccessDenied` return
401/403 because an API must not redirect a `fetch` into Google. The Google
handler is only registered when `GOOGLE_CLIENT_ID`/`SECRET` are present;
everything else keeps working without them. The console deliberately does not
use this cookie.

**Comment threading is flat on the wire.** `ParentCommentId` is a self-reference
with cascade delete. `GetAllComments` returns an ordered flat list and the blog
nests client-side. Timestamps are set centrally by `AppDbContext.SaveChanges*`
for any `ITimestamped`.

## Conventions

- `.env` lives in the solution root (`../.env`) and is parsed by hand at the top
  of `Program.cs` before `CreateBuilder`. Real environment variables win over it,
  which is how the Docker image gets its config. Secrets live there, not in
  `appsettings.json`.
- Authorization checks live inline in `HandleAsync` only when the service has
  already made the rule clear. Keep new endpoints tenant-scoped: admin does not
  mean cross-tenant.
- Field-level failures use `ValidationFailures.Add(...)` and
  `Send.ErrorsAsync`; whole-request failures use `Results.Problem`. Login stays
  deliberately vague to avoid account enumeration.
- Responses are `sealed record`s with a static `From(entity)` mapper, colocated
  in the group file.
- The codebase carries `ponytail:` comments marking deliberate shortcuts with
  their upgrade path. Respect them; do not "fix" them without a measured reason.

## The console

Every page is `@rendermode InteractiveServer` except `Login` and `Register`.
Components run in this process and inject `SiteService` / `CommentService`
directly: no HTTP, no DTO layer, no client-side JavaScript. The auth pages
declare no render mode on purpose because a cookie can only be written during a
real form POST, which an interactive circuit does not give you. Do not add a
render mode to them, and do not remove it from the others.

- `[Authorize]` plus `AuthorizeRouteView`; `RedirectToLogin` handles the
  unauthenticated case because the cookie's `LoginPath` only covers requests
  that are not component renders.
- The current admin's id comes from the cascading `AuthenticationState` through
  `UserClaims.UserIdOf`, the same helper the endpoints use.
- Destructive actions confirm inline with component state, not a JS `confirm()`.
- `Components/Pages/` mirrors the routes; `Layout/` holds the two layouts and
  `ReconnectModal`, which is the only scoped stylesheet.
