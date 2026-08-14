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

The **.NET 10** backend is here, and it is the larger half of the repository — a layered API where the domain project references nothing at all and the application layer references only the domain, so a rule about money cannot quietly come to depend on EF Core or on ASP.NET. Records — products, orders, addresses, articles — are read through one typed data layer on the frontend, each function of which has two paths: the real API call and a mock fallback drawn from the design's own content. Flipping one environment variable switches those over, which is what lets the storefront render on a fresh clone before anyone has a database.

That switch defaults differently in the two directions on purpose. Development starts on the fixtures; production starts on the API and has to be told by name to do anything else. It used to be "fixtures unless the variable says otherwise", which meant a deploy that simply forgot the variable served invented products and prices to shoppers with nothing on screen to say so.

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
- **One signing module, not one per app.** The envelope lives in `@bojan/config/signed-cookie`; the two apps keep only what genuinely differs — cookie name, lifetime, payload, and deliberately different secrets, so a customer cookie can never be replayed as an operator one. They were two copies that had already drifted, which is the shape a security fix reaches three places out of four.
- **No CORS policy, on purpose.** No browser is meant to reach the API: both sites call it from their own Node process over the internal network, carrying a key that has no business in a browser. The only thing loaded cross-origin is `/media`, and an `<img>` is not subject to what CORS relaxes — so the absence of a policy is the boundary, not an oversight.
- **The one-time code has one row per number.** A unique index plus an upsert, so two sign-in requests racing for the same phone leave one live challenge rather than two, and asking for a fresh code returns a fresh attempt count with it.
- **The bearer token is tested as a bearer token.** Every other admin test authenticates the way the panel does, through the proxy headers — so the JWT half of the same policies was minted on every sign-in and never once presented. The first test that presented one found the API validating against a placeholder key, because the key was read a few lines earlier than the issuer beside it.
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

### 👛 A Wallet That Only Holds Money Someone Sent
Wallet balance buys real goods, so crediting it on a customer's say-so is the same as giving the goods away. Nothing in the flow credits a balance except one method, reached two ways.

- **Filing a top-up credits nothing.** It writes a request. The gateway route hands back somewhere to pay and settles on the way back, when the gateway is asked whether the money actually arrived; the card-to-card route files the transfer with its tracking number, date and receipt and sits pending until an operator confirms it against a bank statement. The request that is waiting appears on the wallet screen straight away, marked as such — a transfer that vanishes until someone approves it is how support tickets are made.
- **Card-to-card ships disabled.** It is built and tested, but turning it on commits the shop to somebody reading that queue, and an unattended queue is either ignored or waved through. That is a commercial decision (`Wallet:ManualTopUpEnabled`), not a deployment one, so it defaults to refusing rather than to whatever the environment happens to say.
- **A decision is one-way and happens once.** `Approve` and `Reject` refuse a request that is not pending, so a refreshed callback page and a double-clicked approve button are both no-ops; the customer row is locked before the balance is read, which is what makes that check reliable rather than a race. An operator cannot decide a *gateway* top-up by hand — that would be a way to credit a payment nobody took, using a screen meant for transfers where a human check is the only check there is.
- **The wallet pays what it can.** It used to be all or nothing: a balance one Toman short of the total bought nothing at all. It now contributes the lesser of the balance and the bill, and the gateway collects the difference — one expression rather than three branches, asserted over a table of inputs so the two halves always add up to the bill exactly and never spend money the customer does not have. Only the remainder reaches the gateway; sending the full amount after debiting the wallet would charge its share twice.
- **Orders record what the wallet paid**, because the balance moves on afterwards and a refund has to return what was actually taken.
- Choosing a method that draws on the wallet but cannot collect a shortfall — cash on delivery — says so on the payment step, rather than failing on submit two screens later.

### ✉️ Fourteen Emails, Each One Earned
- **The list came from this shop's events, not another's.** Placed, shipped, delivered — and not "processing" or "packed", which are the shop talking to itself and which teach a reader to ignore the mails that matter. Marketing is deliberately excluded: it needs consent and an unsubscribe link, and that link must never be able to switch off a receipt.
- **No images at all** — not a logo, not a spacer. Clients block them by default so an email built on one arrives broken, and a remote image is a read receipt besides. Tables and inline styles for the same reason: Gmail strips `<style>` blocks and neither it nor Outlook can be relied on for flex or grid.
- **A missing address is a skip, not a failure**, because the main sign-up path is a phone number and an SMS code — and **a send failure never reaches the caller**, because placing an order moved money and must not fail over a mail server.
- **Optional rows are dropped, not printed empty.** An order with no tracking code shows no tracking row; a cancellation with no penalty shows neither the row nor the sentence explaining it. A label beside nothing reads as a field the shop forgot to fill in.
- Amounts in Persian digits with an ASCII separator, reference codes in Latin because they get read down a phone line, dates Jalali in **Tehran's** own day — an order placed after midnight there is the previous evening in UTC.
- It turned up that password reset had been sending the **bare token** as the entire body, and that `NotifiedAtUtc` on the back-in-stock request had never once been set — every "tell me when it is back" was collected and ignored.

### 📬 A Support Inbox In The Panel
- **The mail server is the record.** Nothing is mirrored locally: the inbox reads it on demand and writes replies back into it, so the panel is one more client of the same mailbox rather than a copy that drifts from what an operator sees in their own mail app.
- **Grouped into conversations**, from the inbox *and* the sent folder together, keyed on the outside party plus the subject with any run of `Re:`/`Fwd:`/`پاسخ:` stripped — so a back-and-forth reads as one thread and four unrelated topics from one customer read as four. Tested against the words that merely start like a prefix ("Return", "Refund"), because stripping those would merge unrelated threads.
- **A reply threads and files itself.** It carries the original's `Message-Id`, so the customer's client keeps the exchange together, and it is appended to Sent afterwards — SMTP delivers but does not file, and without that step the answer reaches the customer and vanishes from the shop's own record.
- **Two independent layers on the message body**, because it is text written by anyone who knows the published support address and read by the highest-privileged session in the shop. A server-side allow-list built from empty rather than the library's defaults trimmed, and then a sandboxed frame with **neither** `allow-scripts` nor `allow-same-origin` — so a bypass of the first lands somewhere that cannot execute anything or reach the panel.
- **Remote images cannot survive by any route** — not as a tag, not as an attribute, not through CSS. One in an email is a read receipt and an IP leak to the sender, and the screen says they were blocked rather than quietly showing a body with holes in it.
- **The one secret here is encrypted, not hashed**, because unlike every other secret in this codebase it has to be replayed to an IMAP server. The key ring lives outside the database, and the DTO has no field for it at all — so there is no route by which the panel could render it back.
- Reading sits behind the support section; the settings sit behind owner. Answering customers and holding the credential to the mail account are not the same trust.

### 🔔 Notifications That Actually Arrive
- **The composer's own channel value was wrong.** "اعلان درون‌برنامه‌ای" posted `push`, which parses — `Push` is a real member of that enum — so nothing failed: the campaign was stored as a push campaign, the branch that writes the customer rows never ran, and the dispatcher logged that a channel nobody had chosen had no provider. The screen said the notification was sent and no one ever received one.
- **Nothing called the dispatcher.** It was registered and never invoked, which meant a scheduled campaign was stored and never looked at again, and an SMS broadcast was never sent at all — the only thing that delivered anything was an in-app fan-out inside the request that queued it. A worker drains the queue now, so there is **one delivery path** for every channel and every schedule, and the fan-out is out of the operator's request: an audience of a hundred thousand used to be a hundred thousand inserts in one transaction they waited on.
- **A channel with nothing behind it is refused, not queued and dropped.** Being told the broadcast failed is worth more than a row in a table nobody reads.
- **A notification link cannot leave the site.** It was a comment on the property and nothing checked it; the moment an operator can type one, that is a stored redirect delivered to an inbox — and to the whole customer base on a broadcast. The check is an allow-list of one shape rather than a list of schemes to block, and it also refuses the three things a leading-slash test alone lets through: `//evil.example`, the backslash form browsers normalise into it, and a control character that hides either from a human reading the value back.
- **An operator can notify one customer**, from their record, which the panel could not do before — telling one shopper something about their own order meant broadcasting it to everybody or picking up the phone.
- The bell's badge is a `COUNT` against the index, not the feed loaded and filtered in memory, and it is fetched client-side: the header sits in the root layout, so reading the session to draw it would opt every statically generated page into dynamic rendering.

### 🧾 An Invoice That Bills What The Buyer Kept
- **The number is issued by delivery, not by a screen.** Sixteen random digits minted inside the `delivered` transition itself, so "delivered" and "has an invoice" are one fact rather than two that can drift. Random rather than sequential because a sequential invoice number is a running total of everything the shop has ever sold, readable by anyone who bought twice. Uniqueness is a filtered unique index, and `??=` is what makes it impossible to re-issue a number a customer has already been quoted.
- **Returned goods come off the bill.** Once a return is refunded, the money went back — so those units leave the lines entirely and are reported once, as a count and a sum. Itemising them would invite the reader to add them up; billing them would charge someone for goods they no longer have.
- **Their share of the discount and shipping goes with them**, sliced from the order's own figures rather than re-priced against the smaller basket. A coupon with a minimum spend would price the reduced basket differently from how it was actually charged, and the buyer was charged the original way — subtracting a share is what keeps the invoice footing against the money that moved.
- **One document, two readers.** The panel and the storefront render the same component from the same contract, so the copy the shop files and the copy the customer downloads cannot say different things. What differs is the gate in front of it: ownership for the shopper, the orders section for the operator.
- **It prints as A4, not as a screenshot of a web page.** The zero page margin is deliberate — the browser draws its own header and footer inside that margin and no CSS can remove them, so leaving nowhere to draw them is the removal. The real paper margin comes from the sheet.
- **The shop's own words are settings, not code.** The seller block, the closing note, the footer line and the stamp are edited from *فاکتورها ← تنظیمات فاکتور* — a support address or a legal footer is not a deploy. They resolve **per field**, so setting one does not blank the rest, and a shop that never opens the screen prints the same complete document it always did. They ride on the invoice rather than being fetched beside it: the storefront cannot read an owner-only settings endpoint, and a customer's copy quietly falling back to defaults while the operator's showed the real details would be two different documents.
- **The stamp is the one field with no default.** There is no placeholder seal, because an invented one on a document that settles money is a forgery rather than a placeholder — until a file is uploaded the document draws an empty box to stamp by hand, and the moment one is, the box and its caption disappear. Its upload route is owner-only and separate from the shared one, which admits any role the catalogue policy admits.
- **Every figure on it is Latin.** The invoice is a record that gets filed, e-mailed to an accountant and re-keyed, not interface; the rest of the shop stays Persian as the design specifies. The Jalali calendar is kept and only the numbering system swaps, so the date is still the Persian one.
- Searching finds an invoice by its number typed in **Persian digits**, because that is what an Iranian keyboard produces and the column is ASCII.

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

### 🖥️ Operations, Not Just Screens
- **A live server-status card** on the admin dashboard — process uptime, a directly sampled CPU load, memory, disk, and the database health check — read straight off the running .NET process, not simulated.
- **Maintenance mode actually gates the site.** The switch on screen 150 used to write a setting nothing read; storefront middleware now checks it on every request and rewrites to a branded maintenance page, with a time-boxed bypass cookie for previewing the site while it's on.
- **Live chat**, widget on the storefront and a console in the panel under Support — an anonymous visitor (an opaque id kept client-side, no account required) and an operator polling the same conversation, backed by a real table rather than a third-party embed.
- **A background worker drains the report-export queue.** `POST /admin/reports/export` used to leave every job at `Queued` forever — there was a row for a worker that didn't exist. A polling `BackgroundService` now builds the CSV from the same report queries the dashboard uses and serves it back through an authenticated download route.
- **Taxonomy reads are cached, prices and stock never are.** Categories, brands and collections sit behind a five-minute `IMemoryCache`; product listings and detail stay live on every request, because a cached "in stock" is the one kind of staleness a storefront can't absorb.
- **A second worker sends the broadcasts the panel queues.** The dispatcher had been registered and never called by anything, so a scheduled campaign and every SMS broadcast sat unsent forever while the panel reported them delivered. The fan-out resumes rather than repeats: a batch that fails leaves the rest to the next poll instead of re-sending the same offer to everyone the first attempt reached.
- **Uploads are served, not merely stored.** `LocalFileStorage` had always handed back `/media/…` URLs and nothing answered them — a product photo, a top-up receipt and an invoice stamp all saved successfully and then 404'd.
- **A malformed query parameter is a 400, not a 500.** `?page=abc` threw `BadHttpRequestException`, which carries its own 400, and the exception handler reported 500 for it — so every paged list in the panel answered a typo with a server error and anything watching 5xx counted it as an outage.
- **The admin sidebar collapses to an icon rail**, state shared with the top bar so both track the same width, and persisted across reloads.

### 📦 One Command From A Bare Host To A Site With A Certificate
- **The installer installs what is missing rather than listing it.** Docker if the machine has none; nginx and certbot when a domain was given; ufw rules for ssh and nginx — ssh first, because a rule set that allows http but not ssh is how a remote install ends with nobody able to log back in.
- **It does not finish until the site answers.** The health state compose already tracks is polled rather than slept on: the API migrates and seeds on first boot, and is not ready until it says so itself.
- **Running it twice is safe.** An existing `.env` is never overwritten, only missing keys are added, secrets already generated are left alone, and the seeder skips every table that has rows.
- **The API is not exposed.** Nothing in either browser bundle calls it — every use of `NEXT_PUBLIC_API_BASE_URL` is inside a server-side route handler — so nginx forwards only `/media`, where uploaded product images live and an `<img>` really does fetch. That is what keeps `X-Api-Key` meaningful as proof a request came from one of the two Next.js servers.
- **`b-ui` is the menu afterwards**: status, logs, backups, the operator password, the domain and its certificate. An update dumps the database first and rolls back to the previous commit if the new release does not come up healthy, so a bad release costs minutes rather than the site.
- **No domain, no certificate, no nginx.** On a plain IP there is nothing to certify, so none of it is installed and the ports stay on `127.0.0.1` where an ssh tunnel reaches them.

---

## 📊 Implementation Status

| Area | Status |
|------|--------|
| Design system & tokens | ✅ Complete |
| Shared component library | ✅ 26 components |
| Storefront screens | ✅ 90 of 90 |
| Admin panel screens | ✅ 70 of 70 |
| Route protection & sessions | ✅ Signed cookies, enforced in middleware |
| Cart, wishlist, browsing history | ✅ Persisted, one reducer each |
| Checkout | ✅ Both flows on the shopper's own basket and choices |
| Order cancellation | ✅ Staged penalty, automatic restock, wallet refund |
| Returns | ✅ Operator decides; refund and restock follow the decision |
| Wallet | ✅ Credited only on confirmed payment; pays part of an order |
| Invoices | ✅ Issued at delivery, billed net of returns, configurable, printable |
| Payments | ✅ ZarinPal, Zibal and IDPay behind one port, picked in the panel; callback and reconciliation |
| SMS | ✅ SMS.ir, configured from the panel; service line for codes, own line for campaigns |
| Notifications | ✅ In-app, SMS, email and browser push, queued and resumable, links validated |
| Customer email | ✅ 14 templates wired to their events, sent over the support account |
| Support mailbox | ✅ IMAP/SMTP in the panel, threaded, sanitized |
| Live chat | ✅ Storefront widget and panel console over one table |
| Operator accounts | ✅ Appointed from the panel, roles, forced first-password change, 2FA rescue |
| Customer records | ✅ Every field editable, own customer code, delete where no trading history |
| One users list | ✅ Shoppers and operators together, filtered by role, each row to its own editor |
| One identity | ✅ An operator signs in on the storefront with their panel password and shops |
| Magazine | ✅ Written and published from the panel into the table the site reads |
| Report exports | ✅ CSV, a hand-written XLSX, and PDF with an embedded Persian font — itemised rows, not dashboard totals |
| Notifications | ✅ One screen: write to everyone or to one shopper, search both the recipients and everything already sent |
| Dates | ✅ Jalali everywhere, both apps, storefront and panel |
| Images | ✅ Uploaded through the API on every entity that has one |
| Server log | ✅ File sink on its own volume, read in the panel, one line per request with the actor |
| Backups | ✅ Real `pg_dump` archive plus the uploads tree, version-matched client |
| Tests | ✅ 254 frontend, 621 backend, on a real PostgreSQL |
| .NET 10 backend | ✅ Catalogue, account, checkout, panel, uploads, payments |
| Deployment | ✅ One command: Docker, nginx, TLS, four containers, `b-ui` |

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

A fourth pass ran both applications against a real PostgreSQL database and
clicked through them rather than calling the API directly, which is what
surfaced what curling it with a hand-built body could not: a client-side SKU
check that rejected the seeder's own SKUs on every seeded product; an
image-ownership check (from the pass above) that re-validated a record's
*existing* picture on every save rather than only a newly appearing one, so —
since the whole catalogue links a design-tool host — saving anything about
any product, brand, collection or content entry failed until its picture was
replaced first; a generic admin form that sent a switch field's boolean and a
number field's number as the literal strings a hidden input carries, which
the API's JSON binder rejected outright rather than coercing; and six fields
on the brand and category screens that never read back the record's stored
value, so saving either screen without touching one of them silently blanked
it. Screens that share the same fields — collections, campaigns, content —
were already correct, which is why only these two had gone unnoticed.

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
| Icons | Material Symbols Outlined, subset to the 211 the apps use |
| Tooling | pnpm workspaces, ESLint, Prettier |
| Backend | ASP.NET Core (.NET 10) |
| Database | PostgreSQL 17 |
| Deployment | Docker Compose behind nginx, Let's Encrypt via certbot |

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
│   │   │       │   ├── checkout/  # The guided flow's selections
│   │   │       │   ├── wishlist/
│   │   │       │   ├── browsing/  # Recently viewed + search history
│   │   │       │   ├── chat/    # Anonymous visitor id for the live-chat widget
│   │   │       │   └── mock/    # Design-derived fixtures, deleted once the API lands
│   │   │       └── middleware.ts  # Route protection
│   │   └── admin/               # Back office (port 3001)
│   ├── packages/
│   │   ├── config/              # Tailwind preset, design tokens, security headers
│   │   └── ui/                  # 26 shared components + Persian formatters
│   ├── assets/                  # Upstream icon font, the subset's build input
│   ├── scripts/                 # Icon-font subsetting (1.1 MB upstream -> 60 KB)
│   └── Dockerfile               # Builds either app; APP build-arg picks which
├── backend/                     # ASP.NET Core (.NET 10)
│   ├── src/
│   │   ├── Bojan.Domain/        # Entities and rules; references nothing
│   │   ├── Bojan.Application/   # Use cases and ports; references only Domain
│   │   ├── Bojan.Infrastructure/  # EF Core, queries, adapters for the ports
│   │   └── Bojan.Api/           # Minimal-API endpoints, auth, validation
│   ├── tests/                   # Domain unit tests + in-memory API tests
│   └── Dockerfile               # SDK build stage, ASP.NET runtime stage
├── deploy/
│   ├── install.sh               # Provisioner: Docker, secrets, build, health
│   └── b-ui                     # Management menu + CLI, to /usr/local/bin
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

It asks where the site will live, installs whatever the host is missing to put
it there, generates every secret, and does not finish until the whole thing is
answering:

- **Docker**, if the machine has none.
- **nginx and certbot**, when a domain was given — a vhost for the storefront
  on the name and the panel on `admin.`, a certificate covering both plus
  `www`, and the redirect to https.
- **ufw rules** for ssh and nginx, ssh first.
- **Four containers** — PostgreSQL, the .NET API, the storefront and the admin
  panel — waited on until each reports healthy rather than merely started.

Give it no domain and the web server and certificate are skipped: on a plain IP
there is nothing to issue a certificate against, and the ports stay on
`127.0.0.1` where an ssh tunnel reaches them. `b-ui` adds a domain later and
installs the rest then.

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
sudo bash deploy/install.sh --web-only   # redo the vhost and the certificate
```

### Managing it afterwards

The installer puts a `b-ui` command on the path. Run it bare for a menu — it
shows the domain and whether every container is healthy above the options:

```
Bojan Store — System Management
Domain: bojan.ir   /opt/bojan
Status: all services healthy

  1) Service status and addresses
  2) Follow the logs
  3) Update to the latest release (rolls back if unhealthy)
  4) Change the operator password
  5) Domain management (change address, re-issue certificate)
  6) Renew the TLS certificate now
  7) Back up the database
  8) Restart services
  9) Stop everything (data kept)
  0) Exit
```

Every entry is also a subcommand, for a script or an ssh one-liner:

```bash
b-ui status      # what is running, and where
b-ui logs [svc]  # follow the logs
b-ui update      # pull, rebuild, roll back automatically if unhealthy
b-ui backup      # dump the database to ./backups
b-ui password    # change the operator password
b-ui domain      # change the address, rewrite the vhost, re-issue the cert
b-ui ssl         # renew the certificate now
b-ui stop        # stop everything, keeping the data
```

`b-ui update` takes a database dump before it touches anything, and if the new
release does not come up healthy it restores the previous commit and rebuilds —
a bad release costs a few minutes rather than the site.

### What the installer sets up, and what it leaves to you

Give it a domain and it installs and configures nginx and certbot as well as
Docker: a vhost for the storefront on the domain and the panel on `admin.`, a
certificate covering both plus `www`, the redirect to https, and ufw rules for
ssh and nginx. Give it no domain and it skips all of that — on a plain IP there
is nothing to issue a certificate against, and the ports are on `127.0.0.1`
where an ssh tunnel reaches them, which is the right way to look at a test box.

The panel is a subdomain rather than a path because neither Next.js app sets
`basePath`, and an app served under a prefix it was not built for returns HTML
whose every asset URL is wrong.

**The API is not proxied.** Nothing in either browser bundle calls it — every
use of `NEXT_PUBLIC_API_BASE_URL` is inside a server-side route handler, so the
two Next.js servers reach it over the compose network and the internet does not
need to at all. The single exception is `/media`, where the API serves uploaded
product images that an `<img>` really does fetch, so that path and only that
path is forwarded. This is what makes `X-Api-Key` meaningful as proof that a
request came from one of those two servers.

PostgreSQL is not published at any address.

The public URLs are compiled into the browser bundle at image build time, so
changing them means a rebuild — which is what `b-ui domain` does, after
rewriting the vhost and re-issuing the certificate.

### After the first boot: the gateway and the SMS account

Neither lives in a file. Both are entered in the panel, by the owner, on a
running shop — an API key that arrives through an environment file ends up in a
backup, a shell history and eventually a support screenshot, and a shop that
cannot boot until both are set is a shop whose owner can never reach the screen
that sets them.

Until they are filled in the shop still works: it takes orders on cash on
delivery and wallet balance, and it says plainly on each screen what is missing.

**تنظیمات ← پرداخت.** Pick a gateway — **ZarinPal**, **Zibal** or **IDPay** —
paste its credential, and set the return address to
`https://<your-domain>/checkout/payment/callback`. The field beside the picker
changes with it, because the three do not want the same thing: ZarinPal takes a
36-character merchant id, Zibal a merchant key, IDPay an API key.

That return address is checked against the domain registered on your terminal,
so a mismatch is a named error rather than a mystery. Leave **sandbox** on to
rehearse the whole flow without a card being charged; turn it off to take money.
On Zibal the sandbox swaps the *credential* rather than the host, which is
handled for you — pointing at a different URL there would quietly charge real
cards. **آزمایش اتصال** sends one real payment request that nobody is ever
redirected to, and reports what the gateway said in a sentence.

**تنظیمات ← اعلان مرورگر.** Press **ساخت کلید** once. There is nothing else to
configure and nothing to pay for: a browser that agrees to notifications names
the service it can be reached at, and the shop's only credential is that key
pair. Switch the channel on and customers can enable it per device from their
own notifications screen. Generating a *new* pair disconnects every browser
subscribed under the old one, permanently and with no way to tell them, so that
button asks first.

**تنظیمات ← پیامک.** Paste the sms.ir web-service key, then give the **template
id** for the sign-in code and the **parameter name** inside that template. Those
last two are the ones worth care: a sign-in code goes out on the *service* line
through a template sms.ir has approved, which is the only kind of message that
reaches a number which has blocked advertising SMS — and most numbers have. A
parameter name that does not match the template sends the message with the code
missing, which is why **ارسال پیامک آزمایشی** sends a real one to a number you
type. The **line number** is separate and optional: it is the advertising line
campaigns go out on, and without it sign-in still works and campaigns do not.

If sms.ir is set to restrict your key by IP, allow this server's address in
their panel — that refusal comes back as status `12`, which the screen
translates.

**تنظیمات ← فروشگاه.** The shop's name, contact details, address, social
accounts and the three figures the storefront quotes to shoppers: the
free-shipping threshold, the return window and the delivery estimate. Every one
of them is read by the site. Leave a field empty and the row it fills simply is
not drawn — a footer with no LinkedIn icon rather than one that goes nowhere.

**محتوا.** Four kinds, all of them live on the storefront:

| Kind | Where it appears | Slug |
| --- | --- | --- |
| صفحات ثابت | The policy and guide pages | `terms`, `privacy`, `shipping`, `returns`, `buying-guide`, `size-guide` |
| سوالات متداول | Screen 19, grouped by the category on each question | any |
| بنرها | The home page hero | `home-hero` |
| مقاله‌ها | The magazine | any |

The slug is what connects a page you write to the page a visitor opens, so it
has to match the table exactly. Until you write one, each of those pages shows
the copy the application shipped with — the shop launches with policies rather
than with six blank pages, and the moment you save one it takes over. Use `##`
to start a section; blank lines separate paragraphs.

---

## 🚀 Getting Started

### Prerequisites
- [Node.js](https://nodejs.org/) 22 — 20.19 also works, but 22 is what the image ships
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

Frontend, from `frontend/`:

```bash
pnpm typecheck && pnpm test && pnpm build
```

Backend, from `backend/`:

```bash
dotnet build && dotnet test
```

The backend suite takes upwards of half an hour: most of it hosts the whole API
in memory rather than mocking the layer under the one being tested, and it runs
against **a real PostgreSQL** — one container for the run, migrated once into a
template database and cloned per test class. Docker has to be running. It used
to be SQLite, which meant the row locks the checkout depends on were a no-op and
`FOR UPDATE` had never once been exercised by a test.

Switching between `dev` and `build` needs no cleaning step. The two write
incompatible output, so they are given separate directories — `.next-dev` and
`.next` — and never read each other's leftovers.

`pnpm clean` removes both, and there is one case that needs it: renaming or
deleting a route leaves the types Next generated for the old path behind, and
`tsc` then fails on an import of a file that is no longer there. The error names
a path under `.next/types`, which is the tell that the source is fine and the
build output is stale.

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

1. **Server-side cart, wishlist and history**, moving them out of
   `localStorage`. The endpoints exist; each reducer is one file.
2. **Rate limits that survive a second replica.** They now bucket on an address
   the caller cannot forge, but the windows are still in-process: run two
   replicas and the effective ceiling doubles. That wants a shared store, which
   is a container this deployment does not yet have.
3. **Registration without an enumeration oracle.** Registering a number that
   already has an account has to say so — a form that claims to have created an
   account it did not is worse — so the endpoint confirms the number is known.
   Removing that rather than rate-limiting it means verifying the phone before
   the account exists, which is a change to the sign-up screens.
4. **Gateway refunds.** Cancelling returns the wallet's share automatically;
   what a card paid is still reported back for an operator to settle by hand.
   ZarinPal can reverse a transaction, but only within thirty minutes of it —
   which covers almost none of the cancellations that actually happen — so the
   honest version is a refund request against the panel rather than a call
   pretending to be one.
5. **Product media on the shop's own CDN.** The catalogue still links a
   design-tool host, which is the last external origin the frontend depends on.
   The icon font was the other one and is now self-hosted, subset from 1.1 MB to
   60 KB by `scripts/build-icon-font.mjs`, with a test that fails if any name
   in the source has no glyph in what ships.

---

## 📜 License

Proprietary — © Bojan Store. All rights reserved.
