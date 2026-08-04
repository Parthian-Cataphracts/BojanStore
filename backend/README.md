# Bojan Store API

.NET 10 / ASP.NET Core. `../BACKEND.md` is the contract and the build order —
read it first. This file is how to run what exists, and the decisions it left
open that are now made.

## What is here

| Layer | Project | Contains |
|-------|---------|----------|
| Domain | `Bojan.Domain` | Entities, `Money`, order and return lifecycles. No EF Core, no ASP.NET — plain C#. |
| Application | `Bojan.Application` | Use cases, DTOs, and the ports they need. No EF Core, no ASP.NET. |
| Infrastructure | `Bojan.Infrastructure` | EF Core, Npgsql, JWT, password hashing, storage, the payment gateway, the seeder. Implements Application's ports. |
| Api | `Bojan.Api` | Minimal API endpoints, authentication, authorisation policies, rate limiting, DI wiring. |

All eight phases of `BACKEND.md` are implemented. Every path below was taken
from shipped frontend code, not proposed.

| Phase | Where |
|-------|-------|
| 1 — foundation | `Program.cs`, `Auth/`, `Persistence/` |
| 2 — catalogue reads | `Endpoints/CatalogueEndpoints.cs`, `Queries/CatalogueQueries.cs` |
| 3 — account reads | `Endpoints/AccountEndpoints.cs`, `Queries/AccountQueries.cs` |
| 4 — cart, orders, checkout | `Endpoints/CheckoutEndpoints.cs`, `Application/Checkout/CheckoutService.cs` |
| 5 — public and customer writes | `Endpoints/PublicWriteEndpoints.cs`, `Application/Accounts/AccountService.cs` |
| 6 — admin reads | `Endpoints/AdminReadEndpoints.cs`, `Queries/AdminQueries.cs` |
| 7 — admin writes | `Endpoints/AdminWriteEndpoints.cs`, `Application/Administration/` |
| 8 — uploads, payments, notifications | `Storage/`, `Payments/`, `Notifications/` |

### Two base paths, not one

The storefront's `.env.example` points at `/api` and the panel's at
`/api/admin`, so the host mounts two groups. A resource with the same name
under each is a **different endpoint**: `GET /api/products` is the public
catalogue, `POST /api/admin/products` is the panel's write.

`/health` sits outside both — it is for the panel's status screen and for
whatever watches the process, not for the storefront's data layer.

## The decision `BACKEND.md` left open

Section 1.3 asks for one of two answers to "how does the Next server prove
itself on every call". **Both are implemented, and the reason is the shipped
frontend rather than indecision.**

- **(b) Backend-issued JWT** is the primary scheme. `/auth/otp/verify` and
  `/auth/login` already return a `token`; forwarding it as
  `Authorization: Bearer` opens every endpoint here and would let a mobile
  client in later without a redesign.
- **(a) Shared secret header** exists because today's frontend has no token to
  forward. The panel's write route already sends `X-Admin-User` and the
  storefront's write proxy already sends `X-Customer-Id`. Adding
  `X-Api-Key` to both makes them work unchanged.

The identity headers are honoured **only** when `X-Api-Key` matches
`TrustedProxy:ApiKey`, compared in fixed time. Without the key they
authenticate nobody. An operator's **role** is never taken from a header — it
is read from `admin_users`, so a forged `X-Admin-User` cannot claim `owner`,
and a deactivated operator authenticates as nobody.

The storefront now forwards both. `SessionPayload` carries the token
`/auth/otp/verify` returns, and `lib/api/client.ts` attaches `X-Api-Key` on
every server-side call plus the customer's credential on the ones marked
`auth`. Set `API_KEY` in each app's `.env.local` to the same value as
`TrustedProxy:ApiKey`.

## Running it

### 1. Database

```bash
docker compose up -d
```

PostgreSQL 17 on `localhost:5432` with the credentials
`appsettings.Development.json` already expects (`bojan` / `bojan_dev_only`).
No Docker? Point `ConnectionStrings:Bojan` at any Postgres 16+ instance.

### 2. Apply the schema

```bash
dotnet tool restore
dotnet ef database update --project src/Bojan.Infrastructure --startup-project src/Bojan.Api
```

`dotnet tool restore` installs the exact `dotnet-ef` pinned in
`dotnet-tools.json`, so everyone gets the same one without a global install.

### 3. Run

```bash
dotnet run --project src/Bojan.Api
```

Listens on `http://localhost:7001` in Development — matching the
`API_BASE_URL` both frontend apps' `.env.example` already point at, so there
is no port to reconcile by hand. On an empty database it seeds itself
(`Seed:Enabled` is on in Development), so the catalogue is populated before
the first request.

```bash
curl "http://localhost:7001/api/products?pageSize=3"
```

Run `dotnet run --project src/Bojan.Api --launch-profile https` instead for
`https://localhost:7001` — that profile needs a trusted dev certificate first
(`dotnet dev-certs https --trust`), which is an interactive step the default
profile above deliberately avoids.

### Signing in without an SMS gateway

`09123456789` always receives the code **11111**, and the seeder gives that
number the design's own demo profile — name, email, wallet balance and both
addresses — so the account screens have something to show the moment you are
in. Nothing to set up: clone, run, sign in.

Every other number still gets a real random code, printed to the console by
`ConsoleSmsSender` rather than texted:

```bash
curl -X POST http://localhost:7001/api/auth/otp/request -H "Content-Type: application/json" -d '{"phone":"09121234567"}'
```

**This cannot reach production, and two independent things stop it.**
`Program.cs` registers the decorator that reads `Auth:DevOtp` only when
`IHostEnvironment.IsDevelopment()`, and the settings themselves live only in
`appsettings.Development.json`, which no other environment loads — so
`Auth__DevOtp__Phone` on a production host is read by nothing.

What it changes is narrow, too: only which five digits the challenge was built
from. The challenge row, its five-minute expiry, its five-attempt counter and
the whole verify path are the production ones, so what a developer exercises is
the real sign-in rather than a way around it. Every use logs a warning.

Blank the phone in `appsettings.Development.json` to turn it off, or override it
in `appsettings.Development.local.json` — gitignored — to use a different number.

### 4. Verify

```bash
dotnet build Bojan.slnx
dotnet test Bojan.slnx
```

112 tests: 34 in `Bojan.Domain.Tests` (pure logic — `Money`, `Coupon`,
`Order`, `Product`, `OtpChallenge`) and 78 in `Bojan.Api.Tests`, which drive
real HTTP against the wired-up app. Those cover the OTP round trip, the rate
limiter, the catalogue's DTO shapes, all seven of Phase 4's order rules, the
ownership and role gates including their negative cases, and the seeder against
the real fixture file. See the note below on what running them against SQLite
does and does not prove.

## Invoices

An invoice exists only for a delivered order, and that is one rule rather than
two: `Order.TransitionTo` mints the sixteen-digit number inside the
`Delivered` transition itself, so nothing can be delivered without being
invoiced and nothing can be invoiced without being delivered. `??=` is what
stops a second path re-issuing a number a customer has already been quoted,
and the filtered unique index on the column is the whole of the uniqueness
guarantee — Phonix re-checked each candidate against every existing invoice
because its store kept orders as JSON documents with no index to lean on.

`InvoiceBuilder` (in the domain, free of any storage concern, for the same
reason `OrderCancellation` is) bills what the buyer **kept**. Return requests
that reached `Refunded` take their units off the lines entirely and their share
off the discount and the shipping; requests still under review change nothing,
because no money has gone back yet. The document is rebuilt on every read
rather than frozen at delivery, so a return refunded next month re-renders it
correctly.

| Endpoint | Who | Notes |
|----------|-----|-------|
| `GET /api/admin/invoices` | Orders section | Issued invoices, newest first. `q` matches the invoice number, the order number or the buyer's name — the number through `PersianDigits.ToLatin`, so a Persian-typed `۱۲۳` finds `123`. |
| `GET /api/admin/orders/{id}/invoice` | Orders section | The document. 404 when the order has none. |
| `GET /api/me/orders/{idOrNumber}/invoice` | The order's owner | Same payload, ownership-scoped. One 404 for "not yours" and for "not delivered", so the endpoint is not an oracle for which order numbers exist. |

Both readers get one `InvoiceDto`, unlike orders, which have separate admin and
storefront shapes. There is no field on an invoice an operator may see and the
buyer may not — it *is* the buyer's document — so two shapes would only be two
chances for the shop's copy and the customer's to disagree.

The migration back-fills orders delivered before this existed. Without it they
would sit in a permanent hole: `Delivered` is terminal, so they can never
transition again, and the transition is what issues the number.

## The seed data

`Persistence/Seed/catalogue.json` is the frontend's own fixture set — 33
products, 6 collections, 7 articles, the shipping and payment tiers — lifted
mechanically from `apps/storefront/src/lib/mock/` rather than retyped. The
README calls those fixtures "lifted verbatim from the Stitch design screens";
re-typing them into C# would have guaranteed they drifted.

That is what makes `NEXT_PUBLIC_USE_MOCK_DATA=false` render the same screens as
`true` does, which is `BACKEND.md`'s definition of done for Phase 2.

Two things it deliberately does not invent:

- **No cost price.** It is a panel-only field with no design screen to lift a
  value from, so it stays zero and a margin report reads as "not recorded"
  rather than as a fabricated profit.
- **No operator account** unless `Seed:AdminPassword` is set. The same rule the
  frontend applies to `ADMIN_DEV_PASSWORD`: there is no default password that
  works.

## Configuration

| Key | Where | Notes |
|-----|-------|-------|
| `ConnectionStrings:Bojan` | `appsettings.Development.json` (dev), `ConnectionStrings__Bojan` (prod) | No production default. |
| `Jwt:SigningKey` | Same split | 32+ characters; the app refuses to start otherwise (`JwtOptions.ValidateOnStart`). |
| `TrustedProxy:ApiKey` | Same split | The shared secret both Next apps send. Unset means the scheme authenticates nobody. Never expose it to a browser. |
| `Auth:DevOtp:Phone` / `Auth:DevOtp:Code` | Development only | The fixed sign-in code above. Never read outside Development. |
| `Seed:Enabled` / `Seed:AdminPassword` | Development only | `false` in `appsettings.json`, so a production database is never filled with the demo catalogue by accident. |
| `Storage:RootPath` / `Storage:PublicBaseUrl` | Both | Where uploads land and the URL they are served under. |
| `Payment:GatewayUrl` | Production | Empty means the sandbox gateway is in use — see below. |

## Beyond the eight phases

A few pieces landed after `BACKEND.md`'s phases were all built, closing gaps
a live click-through and an explicit ask surfaced:

| Feature | Where | Notes |
|---------|-------|-------|
| Server status | `Endpoints/AdminReadEndpoints.cs` (`GetSystemStatus`) | `GET /admin/system/status` — uptime, a directly sampled CPU load, memory, disk, and the same database check `/admin/system/health` runs. Samples CPU over ~200ms, so the request costs that much. |
| Maintenance mode | `Application/Common/Ports.cs` (`IStoreStatusQueries`), `Infrastructure/Queries/StoreStatusQueries.cs`, `Endpoints/StoreStatusEndpoints.cs` | `GET /store/status` (public, unauthenticated) reads the `general`/`maintenance` setting the panel's screen 150 switch already wrote — nothing read it before this. The storefront's middleware is the enforcement side. |
| Live chat | `Domain/Support/LiveChat.cs`, `Application/Support/LiveChat*.cs`, `Endpoints/LiveChatEndpoints.cs` | A visitor is an opaque id minted client-side, not a session — same shape as the anonymous support contact form. Public read/send under `/chat`, panel side under `/admin/chat` behind `AdminSupport`. |
| Report export worker | `Infrastructure/Jobs/ReportExportWorker.cs` | `ReportExport`'s own remarks said "a worker fills in `FileUrl`" — there wasn't one, so every export sat at `Queued` forever. This is a polling `BackgroundService`, not a new service to deploy: the .NET-native answer to "add a task queue" for one queue with a handful of rows a minute. Reaches for `IBackupArchiver`, not `IFileStorage` — the latter only accepts the four image types it sniffs by magic bytes. XLSX/PDF formats fail explicitly rather than being served as a mislabeled CSV. |
| Catalogue caching | `Infrastructure/Queries/CachedCatalogueQueries.cs` | A decorator over `CatalogueQueries`, five-minute `IMemoryCache` on categories/brands/collections only. Deliberately not on product listing or detail — price and stock are the two numbers this store cannot show stale. |

## Things worth knowing before touching this

**`Money` is mapped two different ways, on purpose.** A required amount is a
complex property (one `bigint` column, and `Amount` visible to LINQ); an
optional one keeps a value converter. The difference matters because the
panel's reports have to `SUM` and `ORDER BY` those columns in SQL, and a
value-converted property is opaque to LINQ — `o.Subtotal.Amount` has no
translation. See `MoneyValueConverter.cs`.

**The sandbox payment gateway approves everything.** `SandboxPaymentGateway`
plays the role `ConsoleSmsSender` plays for SMS: a real implementation of the
port with the outside world stubbed, so the money path could be built and
tested before a PSP contract exists. It logs a warning on every call, so a
deployment that quietly kept it is visible in the logs rather than only in the
accounts. **`VerifyAsync` returning `true` unconditionally must not survive
into production.**

**Don't add `[Authorize]` without testing the 401 case.** An endpoint that
silently allows anonymous access because the attribute did not take is a much
quieter failure than a compile error. Every protected route here names a policy
from `AuthorizationPolicies`, and the negative cases are tested in
`OwnershipAndRoleTests`.

**Ownership is structural, not a check.** Every `/me` query takes the customer
id and filters on it in the query, so another customer's row is never loaded in
the first place. That is what makes `BACKEND.md` Phase 3's "must 404, not 403"
fall out of the shape rather than out of something to remember.

**A child added to an already-tracked parent must be added through its
repository too.** EF decides the state of an entity discovered through a
tracked parent's navigation by whether its key is already set, and every entity
here assigns its own GUID at construction — so a new row would otherwise be
saved as an `UPDATE` of a row that does not exist. `Order.TransitionTo` and
friends return the entry they appended for exactly this reason.

## Why Api.Tests uses SQLite instead of Postgres

There is no Docker daemon in the environment this was built in, so the
endpoints are verified two ways instead of one:

1. **`Bojan.Api.Tests`** hosts the real app in-process via
   `WebApplicationFactory`, swapping Npgsql for SQLite in-memory, and drives
   the actual HTTP endpoints — real validation, real rate limiting, real
   authorisation, real transactions. This proves the application logic and the
   wiring.
2. **`dotnet ef migrations script`** was reviewed by hand against the generated
   SQL to catch Postgres-specific issues the SQLite run cannot. That is how a
   real bug was caught in Phase 1 before it reached a database: a unique-index
   filter generated with an unquoted, lowercase column reference that would
   have failed against Postgres's quoted columns.

Two places pay for that swap, and both are marked in the code:

- **`ConfigureConventions`** converts `DateTimeOffset` to an ISO 8601 string on
  SQLite only, because SQLite refuses to `ORDER BY` a `DateTimeOffset` at all
  and nearly every read here is ordered by one. Postgres keeps real
  `timestamptz` columns.
- **`PeriodBucket`** writes the report time series' `GROUP BY` once per
  provider, because truncating a timestamp to a day has no portable LINQ form
  through that converter. The database still does the aggregation on both —
  `BACKEND.md` Phase 6's "push these into SQL" holds either way.

**Both migrations have now been applied to a real PostgreSQL 17 and the whole
flow walked against it**: seed, catalogue, OTP sign-in, address, coupon, order
placement with a repeated `Idempotency-Key`, an oversell refusal, the panel's
writes, the audit trail, and every report. Money columns land as `bigint`,
timestamps as `timestamp with time zone`, and Persian round-trips intact.

That run earned its keep. It caught two bugs the SQLite host **structurally
could not**, both in the `FOR UPDATE` lock the SQLite branch skips entirely:
unquoted lowercase identifiers against PascalCase columns, and EF's complex-type
column naming not surviving `FromSql` composition. The lock is now taken by its
own statement, which names only a table and a key.

The gap that remains is the reverse one: the two provider-specific report
queries in `PeriodBucket` have a SQLite branch the suite exercises and a
PostgreSQL branch it cannot. Both were checked by hand against a live database;
neither is covered by a test.

## A version-pinning trap already hit once

The first `dotnet ef migrations add` failed with an opaque
`MissingMethodException` because `Npgsql.EntityFrameworkCore.PostgreSQL` was
pinned to a stale preview against EF Core GA — a binary mismatch NuGet's
resolver does not catch. Central Package Management
(`Directory.Packages.props`) now pins every EF Core-related package to versions
checked against nuget.org. If you add an EF-related package, check its version
actually exists and matches the others before pinning it — the resolver will
restore a mismatched version silently and fail at a much less obvious point.
