<div align="center">

# 🪶 Bojan Store

**A right-to-left Persian retail platform built from a 160-screen design system.**

Next.js 15 · React 19 · Tailwind CSS 3.4 · ASP.NET Core (.NET 10)

[![Next.js](https://img.shields.io/badge/Next.js-15-000000?logo=nextdotjs&logoColor=white)](https://nextjs.org/)
[![React](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.7-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind-3.4-38BDF8?logo=tailwindcss&logoColor=white)](https://tailwindcss.com/)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Screens](https://img.shields.io/badge/screens-160%20of%20160-3fb950)](#-implementation-status)
[![License](https://img.shields.io/badge/license-Proprietary-red)](#-license)

</div>

---

## About

Bojan is an online store for **creative supplies** — stationery, notebooks and planners, art tools, architecture and drafting equipment, and gift and lifestyle goods. It ships as a right-to-left Persian storefront with a full back office behind it.

What is unusual about this repository is where it starts. The entire product was designed before a line of code existed: **160 screens, each drawn twice — once for mobile and once for desktop** — covering the 90-screen storefront and the 70-screen admin panel. That design is the specification. Colours, type scale, spacing and the handful of surface treatments the design reuses everywhere are not invented in code; they are extracted from the source design and committed as a Tailwind preset that both applications share. Nothing hand-picks a hex value.

The two drawings of each screen are not two implementations. Each screen is **one responsive component**: the mobile drawing is the base state and the desktop drawing supplies the `md:` and `lg:` breakpoints. The home page hero is `h-[400px]` and bottom-aligned on a phone and `md:h-[600px]` and centred on a desktop, from the same element. The header renders a 56px mobile bar and an 80px desktop bar from one component. There is no separate mobile site to keep in sync.

Being Persian shapes more than the copy direction. Prices in this design use **Persian digits with an ASCII thousands separator** — `۱,۲۰۰,۰۰۰ تومان` — which is not what `Intl.NumberFormat('fa-IR')` produces, so digits are transliterated deliberately rather than left to the platform. Directional spacing uses logical properties throughout, so nothing needs mirroring by hand. The Latin display faces the design specifies carry no Arabic-script glyphs, so Vazirmatn sits behind them in the font stack and Persian text falls through to it automatically.

The frontend is built to meet a **.NET 10** backend, and built so that meeting it is a configuration change rather than a rewrite. Records — products, orders, addresses, articles — are read through one typed data layer, each function of which has two paths: the real API call and a mock fallback drawn from the design's own content. Flipping one environment variable switches those over.

Pages do still import the fixture module directly for **presentation constants** — status labels and their colour tones, the shipping and payment tiers, the B2B process steps. Those are closer to copy than to data, and they move to the API with the screens that own them rather than through the catalogue layer.

---

## ✨ Core Highlights

### 🎨 Design System Extracted, Not Transcribed
- **Tokens lifted verbatim** from the source design — 47 Material-3 colours, 6 brand aliases, 8 type sizes, the spacing and radius scales — generated into a Tailwind preset rather than typed by hand.
- **One preset, two applications**: the storefront and the admin panel consume the same `@bojan/config` package, so a token change cannot drift between them.
- **The design's own utilities** (`paper-card`, `glass-nav`, `glass-panel`, `hide-scrollbar`, `pb-safe`) are defined once in a shared stylesheet instead of being repeated per screen.
- Colour values carry a *do not hand-edit* contract — the preset is the single source of truth.

### 📱 One Component Per Screen, Two Drawings
- Mobile design is the **base state**; desktop is layered on at `md:` / `lg:`.
- Gutters scale from `margin-mobile` (20px) to `margin-desktop` (64px) through the same container.
- The storefront shows a **five-tab bottom bar** below `md` and the full horizontal nav above it; the panel shows a **four-tab bar and a drawer** below `md` and its 256px sidebar above it. Both navigations read one list, so a new section cannot appear on desktop and go missing on a phone.
- Sticky mobile controls (the product page's buy bar) become inline desktop elements without a second implementation.
- The panel's top bar renders the phone layout below `md` and screen 92's desktop bar above it — one component, split at the breakpoint, like everything else here.

### 🔤 Persian-First Formatting
- **Digit transliteration** to match the design's `۱,۲۰۰,۰۰۰` grouping, which the platform's Persian locale does not produce.
- **Jalali dates** through the platform's Persian calendar, so no conversion table ships.
- **Bidirectional input handling** — Persian and Arabic-Indic digits are normalised back to ASCII before validation, so a user typing `۰۹۱۲…` is not rejected.
- Currency, percentage, phone and timestamp helpers all live in one module and are shared by both applications.

### 🔌 Backend-Ready Data Layer
- **Typed contracts** mirroring the intended .NET DTOs, in one place, ready to be replaced by generated types if the API publishes OpenAPI.
- **Dual-path data access** — every catalogue function calls the real endpoint or falls back to mock content, selected by `NEXT_PUBLIC_USE_MOCK_DATA`.
- **`ApiError` with status** so a 404 is distinguishable from a transport failure at the call site.
- Next.js cache tags and revalidation windows are declared per resource, not sprinkled through pages.
- **Writes are allow-listed, not generic.** Each writable resource declares the fields a form may set; anything else in the body is dropped rather than forwarded, so a crafted request cannot reach a field its form does not show.

### 🔐 Closed by Default
- **Sessions are signed, not merely present.** An HMAC-SHA256 cookie carrying its own expiry, verified on every request — a hand-written cookie does not get in. Web Crypto throughout, so the same module runs in the Edge and Node runtimes.
- **The admin panel denies everything it does not explicitly open.** The middleware matches all paths and exempts sign-in; a new screen is protected the moment it is added, without anyone remembering to list it.
- **Codes and passwords never reach the browser.** OTPs are hashed into a short-lived challenge cookie whose attempt counter is inside the signature, so clearing local state cannot reset it. A wrong password and an unknown account produce the same response.
- **Rate limits on every auth and lookup route**, plus coupon checks — a short code space is otherwise walkable from a browser console. They bucket on the address the *proxy* reported, not the one the caller claimed: `X-Forwarded-For` is a list every proxy appends to, so its left-most entry is written by whoever is being limited. Reading it bought a fresh window per request and made all of these decorative.
- **A password reset ends the sessions open on the old password.** A signed cookie carrying its own expiry cannot be withdrawn, which is why it also carries a security stamp: rotating the account's stamp makes every session minted before it stop authenticating, checked once per request for whichever of the two schemes the caller used.
- **A password alone does not open the panel for an account with two-factor on.** The password step yields a five-minute challenge that authorises nothing — every policy requires a `scope` claim the challenge does not carry — and a second endpoint trades it for a real session once a TOTP code verifies. It used to hand back a working admin token in the same response that said a second factor was required.
- **The role × section permission grid is enforced, not just displayed.** It narrows the four role policies and can never widen them — granting a section a role's own policy forbids still grants nothing, and `owner` is never gated, so the panel's one full-access role cannot be locked out of its own settings by a stray click. An installation that has never opened the screen behaves exactly as it did before the grid existed.
- **Security headers from one shared module** — a source-restrictive CSP, `frame-ancestors 'none'`, `base-uri`/`form-action` locked to the origin, HSTS — applied in `next.config` so statically generated pages are covered too.
- **JSON-LD is escaped, not stringified.** `JSON.stringify` leaves `<` alone, so a title containing `</script>` would close the block early; the payload is made inert while staying valid JSON.

### 🛒 A Basket That Survives
- One reducer owns the cart; no component touches storage. Totals are **derived, never stored**, so a price change cannot leave a stale total behind.
- Quantities are clamped to stock and to a per-line ceiling, a discount can never exceed the goods, and an empty basket owes nothing.
- Stored state is **treated as untrusted** — malformed lines are discarded rather than rendered.
- Orders are re-validated server-side: the address must belong to the signed-in customer, and both method ids must exist. The basket lives in the shopper's browser, so none of it is taken on trust.

### ↩️ Cancelling Costs What The Stage Says
The fulfilment path is payment, initial confirmation, picking from the warehouse, dispatch, delivery. Where a cancellation lands on it decides all three consequences, and those rules live in one place (`OrderCancellation`) rather than as conditions spread through the service that calls them.

- **The penalty starts at the warehouse.** Up to and including the initial confirmation nothing has been spent on the order but a status change, so the balance comes back whole. Once it has been picked and packed that work is real and does not come back with the goods, so a configurable percentage is withheld.
- **The shop cancelling is never penalised.** Out of stock after confirmation, a pricing error — the operator clears one checkbox and the refund is whole however far along the order was. Charging someone for a decision that was not theirs is not a penalty.
- **Stock returns by itself until it is dispatched**, with a movement row naming the order so the inventory screen explains the jump. After dispatch the goods are with a carrier and may not come back at all, so that count is left for an operator to record once the parcel is physically on the shelf — inventing stock is worse than missing it.
- **Cancelling is not a status.** It was one, which meant an operator could cancel an order and leave the customer's money and the shop's stock exactly where they were. It has its own endpoint and its own control, the status endpoint refuses the value outright, and the panel states what pressing the button will do before it does it.
- **Two doors, one implementation.** The operator cancels from screen 95 and the shopper from their own order; they differ in who may do it and in whether the penalty applies, and in nothing else. A stranger's order answers *not found* rather than *forbidden*, so an order that exists is not distinguishable from one that does not.
- **The refund is paid once.** The order row is locked before its status is read, so a double-clicked cancel refunds and restocks once rather than twice — the same guarantee, and the same fix, as the wallet top-up decision.
- The percentage is a setting (*تنظیمات ← سفارش و لغو*), because it is a commercial decision that changes without a deploy. Unset means zero: a shop that has never opened that screen does not quietly start charging people.

### 🔍 Server-Rendered, Shareable Catalogue
- Filter, sort **and page** state live in the URL, so a filtered listing is server-rendered, shareable and back-button correct. Page one stays the bare URL rather than a duplicate of the canonical listing.
- Paging is links, not buttons — on the catalogue, category and search listings, and on the admin tables that carry volume. A page link keeps whatever filters are active.
- Product and category pages are **statically generated** with `generateStaticParams`; listings stay dynamic.
- Per-page metadata and Open Graph output, with the admin panel and transactional pages explicitly excluded from indexing.
- An unknown product, category, article, collection or brand returns a **real 404**, not a 200 carrying a "not found" page.

### 🧩 Shared Component Library
- 24 exported components covering buttons, form fields, cards, badges, ratings, prices, quantity steppers, bottom sheets, tabs, breadcrumbs, skeletons and empty states.
- **Accessible by construction** — icon-only controls require a label, form fields wire up their own error and hint associations, and `Sheet` behaves like a real modal: focus moves in on open, Tab is trapped and wraps at both ends, Escape closes, scroll locks, and focus returns to whatever opened it.
- Styling is exported as a class builder as well as a component, so a `next/link` can look like a button without nesting an anchor inside a button.

### 🧠 State That Belongs to the Shopper
Four things accumulate as the storefront is used, and each still lives in the browser. The API has endpoints waiting for all four; each is one reducer, persisted, with nothing else touching storage — so moving any of them server-side is a single file.

| State | Where it lives | Why |
|-------|----------------|-----|
| Basket | `localStorage` | Survives a reload; the count rides on every cart shortcut |
| Wishlist | `localStorage` | The heart sits on every catalogue card and has to agree everywhere |
| Recently viewed · search history | `localStorage` | Per-person and disposable — losing it costs nothing, so a storage failure is ignored rather than reported |
| Comparison | the URL | Worth sending to someone, and the back button should undo a column added by mistake |

Stored state is parsed defensively in every case: entries that are not shaped like a product or a cart line are discarded rather than rendered.

---

## 📊 Implementation Status

| Area | Status |
|------|--------|
| Design system & tokens | ✅ Complete |
| Shared component library | ✅ 24 components |
| Storefront screens | ✅ 90 of 90 |
| Admin panel screens | ✅ 70 of 70 |
| Route protection & sessions | ✅ Signed cookies, enforced in middleware |
| Cart, wishlist, browsing history | ✅ Persisted, one reducer each |
| Checkout | ✅ Both flows on the shopper's own basket and choices |
| Order cancellation | ✅ Staged penalty, automatic restock, wallet refund |
| Tests | ✅ 164 frontend, 284 backend |
| .NET 10 backend | ✅ Catalogue, account, checkout, panel, uploads, payments |
| Deployment | ✅ One-command installer, four containers, ops CLI |

Every screen in the design has a route. The two applications run standalone
against the design-derived fixtures, and both sign-in flows, the basket, the
wishlist, the coupon check and every form work end to end against them.

The guided checkout (screens 71–80) reads the shopper's own basket and the
choices made on the way through it, as the single-page checkout (screen 08)
does. Three of its steps were quoting a shipping method by a fixed index into
the list rather than the one chosen two screens earlier — 73 and 77 named the
first, 79 named the third — so they disagreed with each other and, on an
unreachable API, crashed on an index that was not there. The summary rail
resolves the chosen method itself now, the way it already read the basket
itself.

The backend covers the catalogue, accounts, checkout, public writes, the
panel's reads and writes, uploads and payments, and seeds itself from the same
design fixtures the frontend renders, so switching `NEXT_PUBLIC_USE_MOCK_DATA`
shows the same screens.

Both applications now forward a credential to it, so the account, order and
B2B screens read live data. The whole flow — sign-in, address, coupon, order,
the panel's writes and reports — has been walked against a real PostgreSQL 17.
Guest order tracking (screen 30) reads the real `GET /orders/track`.

**The admin panel is now fully wired**, not just its reads. Every catalogue
screen (products, brands, categories, collections), coupons, campaigns and
content (articles, banners, FAQ, pages) create and edit against the real API —
this took a second pass after the initial read-layer migration, because several
screens had a working list view backed by fixtures underneath an edit form that
either never fetched the record or submitted a field name the write allow-list
didn't recognize. A full click-through audit against a live backend, screen by
screen, is what found these; reading the code alone did not surface them.

A third pass, adversarial rather than click-through, found what a working UI
does not surface: fields several product, brand, category and collection forms
collected and posted that the request records never declared, so they were
dropped in the deserialiser and the API answered success having saved nothing;
a customer lookup that paged the whole list and 404'd anyone past the newest
two hundred; a card-to-card wallet receipt taken on trust rather than checked
like every other image a customer supplies, with nowhere legitimate to upload
one and no screen that ever showed it to the operator deciding whether to
credit the money; and the permission grid and two-factor gaps above. All of it
is covered by the tests in the count above, not only fixed.

---

## 🏷️ Topics

`ecommerce` · `storefront` · `admin-dashboard` · `design-system` · `design-tokens`
`nextjs` · `react` · `typescript` · `tailwindcss` · `monorepo` · `pnpm`
`dotnet` · `aspnetcore` · `rtl` · `persian` · `i18n` · `responsive-design` · `ssr`

---

## 🧱 Tech Stack

| Layer | Technology |
|-------|------------|
| Framework | Next.js 15 (App Router), React 19 |
| Language | TypeScript 5.7, `strict` with `noUncheckedIndexedAccess` |
| Styling | Tailwind CSS 3.4 via a shared preset |
| Typography | Vazirmatn everywhere; Inter for Latin technical values only |
| Icons | Material Symbols Outlined, subset to the 194 the apps use |
| Tooling | pnpm workspaces, ESLint, Prettier |
| Backend | ASP.NET Core (.NET 10) |
| Database | PostgreSQL 17 |
| Deployment | Docker Compose, one-command installer |

---

## 📂 Repository Structure

```
BojanStore/
├── frontend/
│   ├── apps/
│   │   ├── storefront/          # Customer-facing shop (port 3000)
│   │   │   └── src/
│   │   │       ├── app/         # App Router pages
│   │   │       ├── components/  # Feature components
│   │   │       ├── lib/
│   │   │       │   ├── api/     # Typed .NET client + dual-path data access
│   │   │       │   ├── auth/    # Signed session cookies, rate limiting
│   │   │       │   ├── cart/    # Basket reducer
│   │   │       │   ├── wishlist/
│   │   │       │   ├── browsing/  # Recently viewed + search history
│   │   │       │   └── mock/    # Design-derived fixtures, deleted once the API lands
│   │   │       └── middleware.ts  # Route protection
│   │   └── admin/               # Back office (port 3001)
│   ├── packages/
│   │   ├── config/              # Tailwind preset, design tokens, security headers
│   │   └── ui/                  # Shared components + Persian formatters
│   ├── scripts/                 # Icon-font subsetting (1.1 MB upstream -> 60 KB)
│   └── Dockerfile               # Builds either app; APP build-arg picks which
├── backend/                     # ASP.NET Core (.NET 10) API
│   └── Dockerfile               # SDK build stage, ASP.NET runtime stage
├── deploy/
│   ├── install.sh               # Provisioner: Docker, secrets, build, health
│   └── bojan                    # Operations CLI, installed to /usr/local/bin
├── docker-compose.yml           # PostgreSQL + API + storefront + admin
├── .env.example                 # Every deployment value, secrets left blank
├── install.sh                   # One-line bootstrap; hands off to deploy/
└── README.md
```

---

## 🚀 Deploy to a Server

One command on a bare Ubuntu or Debian host:

```bash
bash <(curl -Ls https://raw.githubusercontent.com/Parthian-Cataphracts/BojanStore/main/install.sh)
```

It installs Docker if the machine has none, asks where the site will live,
generates every secret, then builds and starts four containers — PostgreSQL,
the .NET API, the storefront and the admin panel — and waits until each one
reports healthy rather than merely started.

Run it twice and nothing is lost: an existing `.env` is never overwritten, and
the database seeder skips every table that already has rows.

> Use the `bash <(curl …)` form rather than `curl … | bash`. Piping puts the
> script itself on stdin, and the installer's questions would read the script's
> own text instead of waiting for an answer.

Already have the repository?

```bash
sudo bash deploy/install.sh              # install
sudo bash deploy/install.sh --defaults   # unattended, take every default
sudo bash deploy/install.sh --rebuild    # rebuild images after new code
```

### Managing it afterwards

The installer puts a `bojan` command on the path. Run it bare for a menu, or
give it a subcommand:

```bash
bojan             # interactive menu
bojan status      # what is running, and where
bojan logs        # follow the logs
bojan update      # pull, rebuild, roll back automatically if unhealthy
bojan backup      # dump the database to ./backups
bojan password    # change the operator password
bojan domain      # change the public address and rebuild
bojan stop        # stop everything, keeping the data
```

`bojan update` takes a database dump before it touches anything, and if the new
release does not come up healthy it restores the previous commit and rebuilds —
a bad release costs a few minutes rather than the site.

### What the deployment expects of you

Ports are published on `127.0.0.1` only, and PostgreSQL is not published at
all. Put a reverse proxy in front to terminate TLS and route the two sites.
Nothing here should face the internet directly: the API treats `X-Api-Key` as
proof that a request came from one of the two Next.js servers, and that
assumption is what makes the customer identity those servers assert
trustworthy.

The public URLs are compiled into the browser bundle at image build time, so
changing them means a rebuild — which is exactly what `bojan domain` does.

---

## 🚀 Getting Started

### Prerequisites
- [Node.js](https://nodejs.org/) 20.11 or newer
- [pnpm](https://pnpm.io/) 9 — `corepack enable pnpm`
- [.NET 10 SDK](https://dotnet.microsoft.com/download) — for the API

### Install

```bash
cd frontend && pnpm install
```

### Run the storefront

```bash
pnpm dev
```

### Run the admin panel

```bash
pnpm dev:admin
```

Copy `.env.example` to `.env.local` in each app before the first run. With `NEXT_PUBLIC_USE_MOCK_DATA=true` — the default — both applications run standalone against design-derived fixtures, no backend required.

**Signing in locally.** The storefront takes any valid `09…` number; the SMS
code is printed to the server console rather than sent to the browser (set
`MOCK_OTP_CODE` to change it). Against the real API, `09123456789` always
receives `11111` and comes with a seeded demo account — a Development-only
shortcut no other environment can read; see `backend/README.md`. The admin
panel refuses every sign-in until `ADMIN_DEV_PASSWORD` is set in
`apps/admin/.env.local` — there is no default password that works.

`AUTH_SECRET` signs the session cookies. Development falls back to a fixed
value so `pnpm dev` works out of the box; production has no fallback and the
app refuses to mint a session without one. Give the two applications
**different** secrets, so a customer session can never be replayed as an
operator session.

### Verify

```bash
pnpm typecheck && pnpm test && pnpm build
```

Switching between `dev` and `build` needs no cleaning step. The two write
incompatible output, so they are given separate directories — `.next-dev` and
`.next` — and never read each other's leftovers. `pnpm clean` removes both if
you ever want a cold start.

---

## 🌍 Right-to-Left Notes

The interface is Persian and right-to-left end to end. Two rules keep it that way:

- **Use logical properties for anything directional** — `ps-*`, `pe-*`, `ms-*`, `me-*`, `start-*`, `end-*` — never `pl/pr`, `left/right`. Physical properties do not mirror and will break the layout the moment the direction changes.
- **Never format numbers or dates inline.** Every numeral a user sees goes through `@bojan/ui`, so the digit set and grouping stay consistent with the design.

Code comments and documentation are written in English; user-facing strings are Persian.

---

## ⚠️ One Trap Worth Knowing

A `loading.tsx` makes Next stream the response, which sends the HTTP status
before the page body runs — so a page under that boundary can no longer answer
`404`. `notFound()` still renders, but it arrives inside a `200`, and a soft 404
is worse for search engines than a slow page is for readers.

That is why only `/search` has a loading state: it is the one dynamic listing
with no `notFound()` beneath it. Before adding another, check whether anything
in that subtree can 404, and if it can, put the boundary inside the page around
the slow part rather than in a `loading.tsx` above it.

---

## 🗺️ Roadmap

1. **A real SMS gateway and a real payment gateway.** Both sit behind ports
   with working stubs, so each is one class. The payment stub is gated: the
   API refuses to start if `Payment:GatewayUrl` is set while the sandbox — which
   approves every payment without contacting a bank — is the only adapter
   registered.
2. **Server-side cart, wishlist and history**, moving them out of
   `localStorage`. The endpoints exist; each reducer is one file.
3. **Rate limits that survive a second replica.** They now bucket on an address
   the caller cannot forge, but the windows are still in-process: run two
   replicas and the effective ceiling doubles. That wants a shared store, which
   is a container this deployment does not yet have.
4. **Registration without an enumeration oracle.** Registering a number that
   already has an account has to say so — a form that claims to have created an
   account it did not is worse — so the endpoint confirms the number is known.
   Removing that rather than rate-limiting it means verifying the phone before
   the account exists, which is a change to the sign-up screens.
5. **Gateway refunds.** Cancelling returns the wallet's share automatically;
   what a card paid is reported back for an operator to settle by hand, because
   returning it is a call to a payment provider and the only adapter behind
   `IPaymentGateway` is the sandbox. This lands with the real gateway above.
6. Self-hosted icon subset and product media CDN, replacing the two external
   hosts the frontend currently depends on.

---

## 📜 License

Proprietary — © Bojan Store. All rights reserved.
