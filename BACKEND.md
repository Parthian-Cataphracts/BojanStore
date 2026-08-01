# Backend Roadmap — Bojan Store API

**Read this first, then `README.md`.** The frontend is finished and running
against fixtures. This document is the contract it already expects, split into
phases a team can divide.

Nothing here is a proposal. Every route, field and cookie below is **already
called by shipped frontend code** — the paths were extracted from
`apps/storefront/src/lib/api/`, `apps/storefront/src/app/api/account/[action]/route.ts`
and `apps/admin/src/lib/api/resources.ts`. If you change a name, you change the
frontend too.

---

## 0. The one thing to understand before writing code

Every data function in the frontend has **two paths**:

```ts
export async function getProducts(query = {}) {
  if (useMockData) return /* fixture */;
  return api.get<Paged<Product>>('/products', { query });
}
```

`useMockData` is `process.env.NEXT_PUBLIC_USE_MOCK_DATA !== 'false'`.

**This means you can ship the backend endpoint by endpoint.** There is no big
switchover. The flag is currently all-or-nothing per app, so the practical
sequence is: build a phase, point a local frontend at it with
`NEXT_PUBLIC_USE_MOCK_DATA=false`, fix mismatches, merge.

> If you want per-endpoint switching during the transition, that is a
> **frontend** change: make `useMockData` a function of the resource rather
> than a module constant. Do it in `lib/api/client.ts`, once, rather than
> scattering flags.

### Two base URLs, not one

| App | Env var | Value in `.env.example` |
|-----|---------|------------------------|
| Storefront | `API_BASE_URL` / `NEXT_PUBLIC_API_BASE_URL` | `https://localhost:7001/api` |
| Admin | `API_BASE_URL` / `NEXT_PUBLIC_API_BASE_URL` | `https://localhost:7001/api/admin` |

**All admin paths in this document are relative to `/api/admin`.** A resource
with `path: '/products'` in the admin table is `POST /api/admin/products`, which
is a *different endpoint* from the storefront's `GET /api/products`.

### Who calls the API

Server components and Next route handlers — **not the browser**. The browser
talks to Next, Next talks to you. Two consequences:

- CORS is not your problem for the app itself. Lock it down.
- Your caller is a server with a shared secret or a forwarded session, not a
  public JS client. Design auth accordingly (see Phase 1).

---

## Phase map

| Phase | What | Blocks | Rough size |
|-------|------|--------|-----------|
| 1 | Solution skeleton, database, auth | everything | L |
| 2 | Catalogue reads | storefront browse | M |
| 3 | Customer account reads | account screens | M |
| 4 | Cart, orders, checkout | the money path | L |
| 5 | Public writes (reviews, B2B, support) | forms | M |
| 6 | Admin reads | panel | M |
| 7 | Admin writes | panel | L |
| 8 | Uploads, payments, notifications | the last frontend gaps | M |

Phases 2–5 and 6–7 are two tracks that can run in parallel once Phase 1 lands.
**Phase 1 is the bottleneck — do not split it across people.**

---

## Phase 1 — Foundation

**Owner: one person. Everything else waits on this.**

### 1.1 Solution layout

Create under `backend/` (the folder is referenced in the README and does not
exist yet).

```
backend/
├── Bojan.sln
├── src/
│   ├── Bojan.Api/            # ASP.NET Core host, endpoints, DI, middleware
│   ├── Bojan.Domain/         # Entities, value objects, domain rules. No EF, no ASP.NET.
│   ├── Bojan.Application/    # Use cases, DTOs, validation. Depends on Domain only.
│   └── Bojan.Infrastructure/ # EF Core, repositories, SMS, storage, payment
└── tests/
    ├── Bojan.Domain.Tests/
    └── Bojan.Api.Tests/       # Endpoint tests against an in-memory host
```

```bash
dotnet new sln -n Bojan
dotnet new webapi -n Bojan.Api -o src/Bojan.Api
dotnet new classlib -n Bojan.Domain -o src/Bojan.Domain
dotnet new classlib -n Bojan.Application -o src/Bojan.Application
dotnet new classlib -n Bojan.Infrastructure -o src/Bojan.Infrastructure
```

Reference direction is one-way: `Api → Application → Domain`, and
`Infrastructure → Application`. If `Domain` ever needs a `using
Microsoft.EntityFrameworkCore`, something is in the wrong project.

### 1.2 Database

PostgreSQL. EF Core with migrations checked in.

Decisions to make **now**, because changing them later is expensive:

- **Money.** The frontend `Money` type and every price is an integer number of
  **Toman**, not Rial, and never a decimal. Store `long`/`bigint`. Do not use
  `decimal` and do not store Rial — the frontend formats what you send.
- **Dates.** Store UTC `timestamptz`. Send ISO 8601. The frontend converts to
  Jalali for display; **never send a Jalali string**.
- **Ids.** The frontend treats every id as an opaque `string`. GUIDs are fine.
  Slugs are separate, human-readable, and unique per entity type.
- **Soft delete** on products, categories, orders. The panel's delete actions
  should not lose history.

### 1.3 Auth — read this carefully

The frontend **already implements sessions**. It mints its own signed cookie and
verifies it in middleware. You are not being asked to issue cookies.

What exists today, in `apps/*/src/lib/auth/session.ts`:

| | Storefront | Admin |
|---|---|---|
| Cookie | `bojan_session` | `bojan_admin_session` |
| Lifetime | 30 days | 8 hours |
| Payload | `{ sub, phone, name?, exp }` | `{ sub, name, email, role, exp }` |
| Roles | — | `owner` \| `product` \| `sales` \| `support` |
| OTP challenge cookie | `bojan_otp` | `bojan_admin_otp` |
| OTP window / attempts | 5 min / 5 | 5 min / 5 |
| Login attempts | — | 8 |

**Two endpoints the frontend calls today:**

```
POST /auth/otp/request   { phone }            -> 200, body ignored
POST /auth/otp/verify    { phone, code }      -> { userId, firstName?, lastName?, isNewUser? }
```

`otp/request` must send the SMS. `otp/verify` must validate and return the
customer. The frontend then signs its own cookie from your response.

**Your decision to make:** how the Next server proves itself to you on every
*other* call. Pick one and write it down:

- **(a) Shared secret header.** Next sends `X-Api-Key`, plus the customer id
  from its verified session. Simple; trusts Next completely.
- **(b) Backend-issued JWT.** `otp/verify` returns a token, Next stores it in
  the session cookie and forwards it as `Authorization: Bearer`. More work, but
  the API can be opened to a mobile client later without redesign.

**(b) ages better.** If you pick it, `SessionPayload` gains a `token` field and
`lib/api/client.ts` forwards it — a small, contained frontend change.

Either way: **the API must never trust a customer id that arrives in a request
body.** Derive it from the credential.

### 1.4 Cross-cutting, done once

- **Error shape.** The frontend throws `ApiError(message, status, body)` on any
  non-2xx and shows its own Persian copy. Return RFC 7807 `ProblemDetails`.
  **Never return a Persian string** — the frontend owns user-facing text.
- **`204 No Content`** is handled; return it for writes with nothing to say.
- **Paging.** `Paged<T>` is `{ items, total, page, pageSize }`. Every list
  endpoint that the frontend pages must return all four.
- Serialise `camelCase`. Validate with FluentValidation. Serilog to stdout.
- Health endpoint for admin screen 157 (`/health`).

**Deliverable:** `dotnet run` serves `/health`, migrations create the schema,
and both OTP endpoints work end to end against a real SMS provider stub.

---

## Phase 2 — Catalogue reads

Storefront, public, cacheable. The frontend already declares cache tags and
revalidation windows per resource — respect `ETag`/`Cache-Control` and it will
behave.

| Method | Path | Returns | Notes |
|--------|------|---------|-------|
| GET | `/products` | `Paged<Product>` | Query below |
| GET | `/products/{slug}` | `Product` | 404 if unknown |
| GET | `/products/{slug}/related` | `Product[]` | `?limit=` |
| GET | `/products/compare` | `Product[]` | `?slugs=a,b,c` |
| GET | `/categories` | `Category[]` | |
| GET | `/brands` | `Brand[]` | |
| GET | `/collections` | `Collection[]` | |
| GET | `/collections/{slug}/products` | `Product[]` | |
| GET | `/articles` | `Article[]` | `?category=` |

**`ProductQuery`** — the exact params the listing sends:
`category`, `brand`, `search`, `minPrice`, `maxPrice`, `inStockOnly`,
`sort`, `page`, `pageSize`.

`sort` values in use: `newest`, `bestselling`, `price-asc`, `price-desc`,
`rating`. Default `pageSize` is 24.

**DTO source of truth:** `apps/storefront/src/lib/api/types.ts`. Mirror it
field for field. `Product` is the one to get exactly right — it appears in nine
different screens.

**Deliverable:** set `NEXT_PUBLIC_USE_MOCK_DATA=false` and the catalogue,
category, search, collection, brand and magazine screens all render from
Postgres.

---

## Phase 3 — Customer account reads

All require a session. All are per-user — `Cache-Control: no-store`.

| Method | Path | Returns |
|--------|------|---------|
| GET | `/me` | `User` |
| GET | `/me/orders` | `OrderSummary[]` (`?status=`) |
| GET | `/me/orders/{id}` | `OrderDetail` |
| GET | `/me/addresses` | `Address[]` |
| GET | `/me/addresses/{id}` | `Address` |
| GET | `/me/wishlist` | `Product[]` |
| GET | `/me/returns` | `ReturnRequest[]` |
| GET | `/me/returns/{id}` | `ReturnRequest` |
| GET | `/me/notifications` | `Notification[]` |
| GET | `/me/support/tickets` | `SupportTicket[]` |
| GET | `/me/reviews` | `MyReview[]` |
| GET | `/me/reviews/awaiting` | `AwaitingReview[]` |
| GET | `/me/wallet/transactions` | `WalletTransaction[]` |
| GET | `/me/coupons` | `Coupon[]` |
| GET | `/me/recently-viewed` | `Product[]` |

`/me/orders/{id}` and `/me/returns/{id}` accept **either** the id or the
human-readable number/code — the frontend passes whichever it has.

**Ownership check on every one.** `/me/orders/{id}` must 404, not 403, for an
order belonging to someone else — a 403 confirms the order exists.

---

## Phase 4 — Cart, orders, checkout

**The money path. Most careful work in the project.**

### The cart is currently in the browser

`localStorage`, one reducer, in `apps/storefront/src/lib/cart/store.tsx`. It
already re-validates server-side at checkout. Two options:

- **Keep it client-side for v1.** Nothing to build. `POST /orders` receives the
  lines and re-prices them.
- **Move it server-side.** Better (survives device changes, enables abandoned-cart
  work). Needs `GET/POST/PATCH/DELETE /cart`. The frontend change is one file.

**Recommendation: keep client-side for v1, move in v2.** It is not on the
critical path and Phase 4 is already the biggest phase.

### Endpoints the frontend calls today

```
POST /cart/coupon   { code }   ->  { code: string, discount: number }
POST /orders        (below)    ->  { orderNumber: string, paymentUrl?: string }
```

`discount` is an **absolute amount in Toman**, not a percentage — the frontend
subtracts it as given. An invalid or expired code is a non-2xx, not a
`{ valid: false }` body.

`POST /orders` body, exactly:

```ts
{
  lines: Array<{ productId: string; quantity: number }>,
  addressId: string,
  shippingMethodId: string,
  paymentMethodId: string,
  couponCode?: string,
  note?: string,
}
```

Note what is **not** there: no prices, no totals. The basket comes from the
shopper's browser, so the frontend deliberately sends only ids and quantities
and expects you to price it. Do not add prices to this contract.

One more read, called on every checkout screen:

```
GET /shipping-methods  ->  Array<{ id, title, price, … }>   (cached 1h)
```

**`POST /orders` — the rules the frontend already enforces and you must
re-enforce, because the basket comes from the shopper's browser:**

1. **Re-price every line from the database.** Never trust a submitted price.
2. **Re-check stock**, and reserve it within the transaction.
3. **The address must belong to the caller.**
4. **Both `shippingMethodId` and `paymentMethodId` must exist.**
5. **Re-apply the coupon** — validity, minimum spend, expiry, per-customer use.
6. Discount can never exceed goods total. An empty basket is a `400`.
7. Idempotency: accept an `Idempotency-Key` header. A double-submitted order is
   the single worst bug this system can have.

Return `paymentUrl` when payment is a gateway redirect — **the checkout already
redirects to it when present.** Return it absent for cash-on-delivery.

### Also in this phase

```
GET  /orders/track?number=…&phone=…   -> OrderSummary | 404   (public, rate-limited)
```

**Not yet wired on the frontend** — screen 30 looks up the fixture and carries a
`TODO` in `components/status/TrackOrderForm.tsx`. Unlike everything else in this
document, this one needs a small frontend change too when you build it.

Rate-limit it hard and server-side. Matching on number **and** phone is what
stops it being an order-number enumeration vector; do not let a number alone
return anything.

---

## Phase 5 — Public and customer writes

The frontend posts these through **one allow-listed proxy**
(`/api/account/[action]`), which drops any field not on the list before
forwarding. The list below **is** the accepted body. Anything else never
reaches you.

| Path | Fields | Session? | FE limit |
|------|--------|----------|----------|
| `PUT /me` | `firstName, lastName, email, birthDate, city, nationalId` | yes | 10/min |
| `POST /me/addresses` | `id, title, recipient, phone, province, city, postalCode, line, isDefault` | yes | 20 |
| `POST /me/addresses/delete` | `id` | yes | 20 |
| `POST /reviews` | `productSlug, rating, title, body, recommend` | yes | 5 |
| `POST /questions` | `productSlug, body` | yes | 5 |
| `POST /me/returns` | `orderId, items, reason, description, refundMethod` | yes | 5 |
| `POST /me/notifications/read` | `ids` | yes | 30 |
| `POST /me/wishlist/remove` | `productId` | yes | 30 |
| `POST /me/search-history/clear` | `all` | yes | 10 |
| `POST /stock-alerts` | `productSlug, phone, email` | no | 5 |
| `POST /support/messages` | `name, phone, email, subject, body` | no | 3 |
| `POST /business/requests` | `organization, contact, phone, email, items, description, deadline` | no | 3 |
| `POST /business/bulk-orders` | `organization, contact, phone, email, items, note` | no | 3 |
| `PUT /business/organization` | `organization, registrationNumber, economicCode, province, city, address, phone, email` | yes | 10 |

Field values are capped at 2000 characters by the proxy. Reviews and questions
need a moderation state — the panel has screens for it.

B2B reads, also this phase: `GET /business/requests`, `/business/quotes`,
`/business/quotes/{id}`, `/business/gift-bundles`.

---

## Phase 6 — Admin reads

**Everything below is under `/api/admin`.** Separate credential, separate
authorisation.

The panel currently reads fixtures directly — **it has no read layer yet**.
Building it is a small frontend task that should happen alongside this phase:
mirror `apps/storefront/src/lib/api/` into `apps/admin/src/lib/api/`.

List endpoints the panel needs, all `Paged<T>` with `page`, `pageSize`, plus the
filters each screen shows: `/orders`, `/products`, `/customers`, `/inventory`,
`/business-requests`, `/coupons`, `/campaigns`, `/content`, `/support/threads`,
`/settings/audit`.

Dashboard and reports (screens 92, 133–140) need aggregates: sales by period,
order counts by status, top products, customer growth, stock levels, campaign
performance, financial totals. **Push these into SQL** — do not fetch rows and
sum them in C#.

Role gates are already declared per resource in
`apps/admin/src/lib/api/resources.ts`. Reads should use the same gates.

---

## Phase 7 — Admin writes

Same allow-list mechanism, same guarantee: the panel forwards only these fields.
`roles` is the permission the panel already enforces — **enforce it again.**

| Path | Fields | Roles |
|------|--------|-------|
| `/products` | `id, title, slug, …` | owner, product |
| `/products/pricing` | `id, price, costPrice, compareAtPrice` | owner, product |
| `/products/discount` | `id, percent, amount, startsAt, endsAt` | owner, product |
| `/categories` | `id, title, slug, parentId, description, icon, status` | owner, product |
| `/brands` | `id, title, slug, description, logo, status` | owner, product |
| `/collections` | `id, title, slug, description, cover, status` | owner, product |
| `/content` | `id, title, slug, kind, body, excerpt, cover, status` | owner, product |
| `/campaigns` | `id, title, kind, status, startsAt, endsAt, description` | owner, product |
| `/coupons` | `id, code, percent, amount, minimumSpend, expiresAt, status` | owner, sales |
| `/inventory/movements` | `productId, kind, quantity, reason, reference` | owner, product |
| `/orders/status` | `id, status, note, trackingCode` | owner, sales, support |
| `/business-requests` | `id, status, assigneeId, note` | owner, sales |
| `/support/replies` | `threadId, body` | owner, support |
| `/support/canned-replies` | `id, title, body, deleted` | owner, support |
| `/notifications` | `channel, audience, title, body, scheduledAt` | owner, sales |
| `/reports/export` | `report, format, from, to` | all |
| `/settings` | `section, values` | owner |
| `/backups` | `kind, confirm` | owner |
| `/settings/api-keys` | `id, label, scope, revoked` | owner |
| `/me/password` | `currentPassword, newPassword` | all |
| `/me/2fa` | `code, secret` | all |

Notes:

- **`costPrice` is owner/product only and must never appear in a storefront
  response.** It is in the write list because the pricing screen sets it.
- `/orders/status` drives customer notification — decide whether the transition
  or a separate job sends it.
- `/reports/export` queues and mails a link; it is not a synchronous download.
- **Every write here goes in an audit log** — screen 147 displays it.

---

## Phase 8 — What the frontend is still waiting on

These are dead controls today, disabled for a reason. Each needs backend before
the frontend can finish.

| Frontend | Needs | Screen |
|----------|-------|--------|
| Wallet top-up | Payment gateway | 58 |
| Avatar change | Multipart upload | 16 |
| Backup download | A file to serve | 156 |
| Product images | Multipart upload | 105 |
| Return-request photos | Multipart upload | 35 |
| B2B attachments | Multipart upload | 61 |

**Uploads are one shared decision, not six.** Pick direct-to-storage with a
signed URL, or through-the-API. Doing it once unblocks all of them.

---

## Definition of done, per phase

A phase is finished when:

1. Endpoints return the exact DTO shape in `types.ts`.
2. `NEXT_PUBLIC_USE_MOCK_DATA=false` renders the affected screens correctly.
3. Ownership and role checks are tested, including the negative case.
4. Errors are `ProblemDetails`, never Persian text.
5. Money is integer Toman; dates are ISO 8601 UTC.

---

## Known frontend gap to fix alongside

**The guided checkout (screens 71–80) still renders the fixture basket**, while
the cart and single-page checkout read the real one. Eight files in
`apps/storefront/src/app/checkout/`, same change each: read the cart store
instead of importing `mockCart`. Frontend-only, no backend needed — good
warm-up task for whoever joins.

---

## Quick reference

| What | Where |
|------|-------|
| DTO shapes | `apps/storefront/src/lib/api/types.ts` |
| Storefront endpoints | `apps/storefront/src/lib/api/*.ts` |
| Storefront write allow-list | `apps/storefront/src/app/api/account/[action]/route.ts` |
| Admin write allow-list + roles | `apps/admin/src/lib/api/resources.ts` |
| Session/cookie contracts | `apps/*/src/lib/auth/session.ts` |
| Route protection | `apps/*/src/middleware.ts` |
| Order validation already enforced | `apps/storefront/src/app/api/orders/route.ts` |
