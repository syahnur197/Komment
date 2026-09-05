# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A self-hostable comment backend for static blogs, whatever generator built
them. The blog is a separate static site on another origin; this API serves its
comments over CORS with a cross-site session cookie. Part of the `Komment.slnx`
solution.

Two kinds of client reach it, and the difference runs through the auth code:
blogs are pages on someone else's origin whose readers carry a cross-site
session cookie, and `../Dashboard` is a browser app that carries a bearer token.

`../AppHost` is an Aspire host that launches this API plus `../Dashboard` (the
admin console that consumes this API) and wires a `.WithReference` from Blazor
to `backend`. Nothing in this project depends on Aspire — no ServiceDefaults
reference, no telemetry wiring — so `dotnet run` here is still the fast path;
`dotnet run --project ../AppHost` is for when you want both apps and the Aspire
dashboard.

## Commands

```bash
dotnet run                                  # https://localhost:7017 + http://localhost:5147 (needs a local Postgres)
dotnet run --project ../AppHost             # this API + Dashboard + Aspire dashboard; starts Postgres in a container
dotnet build
dotnet ef migrations add <Name>             # after any entity/DbContext change
dotnet ef database update                   # or just start the app — it migrates on boot
```

Swagger UI is at `/swagger` in Development only. There is no test project yet.

`JWT_SIGNING_KEY` is required outside Development — the app refuses to start
without it, because a default in the source would let anyone who read it mint
admin tokens. Development generates a random key per process, so tokens do not
survive a restart.

## Architecture

**FastEndpoints (REPR), not controllers.** One file per endpoint under
`Features/<Area>/`, each holding its request DTO, its `Validator<TRequest>`, and
the `Endpoint<,>` class. `AddFastEndpoints()` scans the assembly at startup —
there is no route table, and validators are discovered by request type. Adding
an endpoint means adding a file, nothing else.

**Groups own the route prefix and the default auth.** `CommentGroup`
(`/api/comment`) is open for writes-behind-cookie with reads opting out via
`AllowAnonymous()`; `SiteGroup` (`/api/site`) requires the `site-admin` role on
every endpoint. Auth endpoints are ungrouped and declare full paths.

**Dependencies come in by property injection** (`public AppDbContext Db { get; set; } = default!;`),
not constructors.

**Sites are tenants and also the CORS allowlist.** `SiteOrigins.IsAllowed` reads
the `Sites` table per preflight, so registering a blog is `POST /api/site` rather
than a redeploy. The same table backs `SafeReturnUrl`, which is the open-redirect
guard on the OAuth callback. The admin console is the one origin that is *not* a
row in that table — nobody registers it, so it comes from `DASHBOARD_ORIGIN`
(comma-separated; dev and production are different origins for the same app).

**Two account kinds, one `Users` table.** Readers sign in with Google
(`GoogleId` set); site admins register with username/password (`IsSiteAdmin`,
PBKDF2 via `PasswordHasher<User>`). Both paths build claims through
`UserClaims.For`/`UserClaims.UserId` so endpoints read the user id off the
principal and never re-look-up. `MULTI_TENANCY` (env) decides whether
registration stays open or closes after the first admin.

**Two credentials, and no endpoint knows which it got.** A `"smart"` policy
scheme (`Program.cs`) forwards to JWT bearer when the request carries
`Authorization: Bearer` and to the cookie otherwise. Because `Tokens.Create`
signs the very claims `UserClaims.For` produces, `Roles()`, `UserIdOf` and every
inline ownership check work identically either way — which is why adding token
auth changed no endpoint. Two traps live here: `DefaultSignInScheme` must name
the cookie scheme (a policy scheme cannot receive the Google handler's sign-in),
and `MapInboundClaims` is off so validation does not rename claims on the way
back in.

**Cookie config is deliberate and load-bearing:** `SameSite=None` + `Secure`
because the blog is cross-origin, and `OnRedirectToLogin`/`AccessDenied` are
overridden to return 401/403 — an API must not redirect a `fetch` into Google.
The Google handler is only registered when `GOOGLE_CLIENT_ID`/`SECRET` are
present; everything else keeps working without them. The Dashboard deliberately
does *not* use this cookie: a third-party cookie is what Safari blocks and
Firefox partitions, which is the whole reason tokens exist.

**Comment threading is flat on the wire.** `ParentCommentId` self-reference with
cascade delete; `GetAllComments` returns an ordered flat list and the blog nests
client-side. Timestamps are set centrally by `AppDbContext.SaveChanges*` for any
`ITimestamped`.

## Conventions

- `.env` lives in the solution root (`../.env`) and is parsed by hand at the top
  of `Program.cs` *before* `CreateBuilder`; real environment variables win over
  it, which is how the Docker image gets its config. Secrets live there, not in
  `appsettings.json` (which holds only a localhost Postgres fallback — AppHost
  and compose both override `ConnectionStrings:comments`).
- Authorization checks live inline in `HandleAsync` (author-or-site-owner for
  comment delete, owner-scoped queries for every site endpoint). Keep new
  endpoints scoped the same way — admin does not mean cross-tenant.
- Field-level failures use `AddError(...)` + `Send.ErrorsAsync`; whole-request
  failures use `Results.Problem`. Deliberately vague on login to avoid account
  enumeration.
- Responses are `sealed record`s with a static `From(entity)` mapper, colocated
  in the group file.
- The codebase carries `ponytail:` comments marking deliberate shortcuts with
  their upgrade path. Respect them; do not "fix" them without a measured reason.
