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

432 tests: 104 in `Bojan.Domain.Tests` (pure logic — `Money`, `Coupon`,
`Order`, `Product`, `OtpChallenge`, `OrderCancellation`, `InvoiceBuilder`, the
notification link rules) and 328 in `Bojan.Api.Tests`, which drive real HTTP
against the wired-up app.

Those cover the OTP round trip, the rate limiter, the catalogue's DTO shapes,
all seven of Phase 4's order rules, the ownership and role gates including their
negative cases, the seeder against the real fixture file, the invoice
arithmetic end to end, and every notification and email path — including the
ones that must **not** fire: an order status that is not news, a stock alert
already sent, a customer with no address.

The mail HTML sanitizer is tested against real payloads rather than a
description of what it should block. See the note below on what running these
against SQLite does and does not prove.

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

### What the owner can change

The parts of the document that are the shop's words rather than the order's
facts — the seller block, the closing note, the footer line and the electronic
stamp — live in the `invoice` settings section and are edited from
*فاکتورها ← تنظیمات فاکتور*. `InvoiceSettingsDto.From` resolves them **per
field**, so an owner who sets only a support address does not lose the rest of
the document to blanks, and a key this version does not know about is ignored
rather than fatal. A shop that has never opened the screen prints the same
complete document it always did.

They are carried on `InvoiceDto` rather than fetched beside it. The panel could
read the settings section directly; the storefront cannot, since that endpoint
is owner-only — and a customer's copy that quietly fell back to defaults while
the operator's showed the real seller details would be two different documents.

The stamp is the one field with no default. There is no placeholder artwork: an
invented seal on a document that settles money would be a forgery rather than a
placeholder, so until a file is uploaded the document draws an empty box to
stamp by hand. It is uploaded through `POST /admin/uploads/invoices`, which is
**owner-only** and deliberately separate from the `{folder}` route — that one
admits any role the catalogue policy admits and narrows by section only once
the permission grid has been configured, so on an installation that never
opened screen 146 a product operator could otherwise have replaced the mark the
shop signs its invoices with.

Every figure on the printed document is set in Latin digits. It is a financial
record that gets filed and re-keyed, not interface; the rest of the shop stays
Persian, as the design specifies.

## Notifications

`POST /admin/notifications` queues a `NotificationCampaign`; `NotificationWorker`
drains the queue and `INotificationDispatcher` turns each one into whatever its
channel means. One path for every channel and every schedule.

It did not work. Three things, each of which looked fine from the panel:

- The composer posted `push` for its in-app option. `Push` parses, so the
  campaign was stored as a push campaign, the in-app branch never ran, and
  nobody received anything.
- **Nothing called the dispatcher.** It was registered and never invoked, so a
  scheduled campaign and every SMS broadcast sat unsent forever — the only
  delivery that happened at all was an in-app fan-out inline in the queueing
  request.
- A channel with no provider was accepted, stored, and dropped at dispatch with
  a log line. `Email` and `Push` are refused at queue time now.

Taking the fan-out out of the request matters beyond tidiness: it wrote one row
per customer in a single `SaveChanges`, so the tracker held every entity until
it completed and a failure anywhere lost the lot. The dispatcher batches, and
stamps the campaign sent only after the last batch lands.

**Batching alone would have traded that for something worse.** A batch that
fails leaves the earlier ones committed with the campaign still unstamped, so
the next poll starts again from the top and sends the same offer a second time
to everyone the first attempt reached. `CustomerNotification.CampaignId` is what
makes the retry *resume*: the dispatcher skips recipients that already have a
row, and a filtered unique index on `(CustomerId, CampaignId)` enforces it when
two dispatches overlap.

The SMS branch is at-least-once and knowingly so — an SMS leaves no row behind,
so there is nothing to resume from. With `ConsoleSmsSender` that costs a
duplicate log line; with a real gateway it would cost money, and the fix is a
per-recipient delivery record that belongs with the gateway work.

**A link on a notification is validated in the domain.**
`CustomerNotification.Href` has a private setter and `WithLink` is the only way
in, so every path that sets one is covered rather than the three that happen to
exist today. `IsInternalPath` allow-lists one shape — a single leading `/` —
because `javascript:` and `data:` are the two everyone thinks of and a browser
knows dozens more. It also refuses `//evil.example` (protocol-relative, leaves
the site while looking like a path), `/\evil.example` (browsers normalise the
backslash into the same thing), and control characters (they hide either from a
human reading the value back).

`POST /admin/customers/notify` sends one in-app notification to one customer.
Deliberately not a broadcast with an audience of one: no channel, no schedule,
it carries a link, and it leaves no campaign row behind — hundreds of them would
make the campaign reports read as if the shop ran hundreds of campaigns. It sits
under the **customers** section and the orders policy, while the broadcast stays
under **campaigns** and the sales policy: a message about one person's account
is not the same trust as one that reaches the entire customer base.

`GET /me/notifications` is capped (50 by default, 200 ceiling, `limit` clamped
rather than trusted) and `GET /me/notifications/unread-count` is a `COUNT`
against the index — it is polled from the header on every page, so it must not
load rows to count them. Marking all read posts **no ids**, which the API reads
as "all": posting the loaded ones would clear only what the capped feed was
holding and leave a badge the button appeared to have cleared.

## A malformed request is not a server error

`?page=abc` against any endpoint with a typed query parameter returned **500**.
The framework throws `BadHttpRequestException`, which carries its own 400, and
`UseExceptionHandler` treats every exception alike — so every paged list in the
panel answered a typo with a server error, and anything watching 5xx counted it
as an outage.

The handler for it sits **after** `UseExceptionHandler` in `Program.cs`, which
means inside it: middleware registered earlier wraps what follows, so a handler
that catches this would otherwise sit outside the one that has already written
the 500. Being inner also means anything it does not catch still reaches the
exception handler and the log untouched.

## Transactional email to customers

Fourteen messages, and the list came from Bojan's own domain rather than from
copying another shop's: the events a customer acts on. Placed, shipped,
delivered — not "processing" or "packed", which are the shop talking to itself
and which train a reader to ignore the ones that matter.

`EmailShell` is the frame: tables and inline styles, because Gmail strips
`<style>` blocks and neither it nor Outlook can be relied on for flex or grid.
The palette is the shop's own tokens, so an email looks like it came from the
same place as the site. **No images at all** — not a logo, not a spacer.
Clients block remote images by default, so an email built on them arrives
broken, and a remote image is a read receipt besides.

Every value goes through `Escape`. Most come from the shop's own data, but a
product title, a cancellation reason and a customer's own name are all text
somebody typed, and a mail client renders what it is given.

Two rules hold everywhere, and `ICustomerMailer` is the reason they are one
place rather than fourteen:

- **A missing address is a skip, not a failure.** The main sign-up path is a
  phone number and an SMS code, so `Customer.Email` is genuinely optional.
- **A send failure never reaches the caller.** Placing an order moved money and
  reserved stock; it must not fail because a mail server is down, and the
  customer has an in-app notification either way.

Amounts are Persian digits with an ASCII separator — the storefront's own
convention, not what `fa-IR` produces. Order and invoice numbers stay Latin:
they get read down a phone line and typed back in. Dates are Jalali in Tehran's
own day, because an order placed after midnight there is the previous evening
in UTC and a receipt dated a day early is the kind of wrongness nobody reports.

`Email:Site` is where the links point. It has to be configured rather than
derived from the request: mail is composed on a worker where there is no
request.

Three things this turned up. Password reset sent the **bare token** as the
whole body — a string of hex with no link and nothing to do with it.
`StockAlert.NotifiedAtUtc` had existed since the entity was written and nothing
ever set it, so every "tell me when it is back" request was collected and
ignored. And `IEmailSender` had nowhere to put an HTML alternative.

## Email and phone verification

Neither address was ever proven. A shopper who signs in with phone + SMS has
their number implicitly attested by the code they just typed — but nothing
recorded that, and a shopper who registered with a password never proved
their number at all. An email address, on either path, was pure self-report.

`Customer.IsEmailVerified` / `IsPhoneVerified` close that gap, and an account
created through the OTP sign-in path starts with `IsPhoneVerified = true` —
receiving that first code already proved the number, and asking for it again
on day one would only train shoppers to ignore the prompt.

**Phone verification reuses `OtpChallenge`** rather than a second table: same
hashed code, same two-minute expiry, same five-attempt cap, same resend
cooldown that already existed for sign-in. The only addition is `Purpose`
(`SignIn` / `PhoneVerification`) and a nullable `CustomerId`, because a login
challenge and a verification challenge for the same number used to collide on
the unique index — the index moved to `(Phone, Purpose)` for exactly that
reason. `POST /account/phone/verify/request` sends a code to the account's
current number; `POST /account/phone/change/request` sends one to a candidate
number instead. Either way, `POST /account/phone/verify/confirm` is the same
endpoint, and the number only actually changes at the moment the code is
confirmed — never at the moment it was requested, and uniqueness is
re-checked at confirm time too, since the gap between the two requests is
exactly where someone else could have claimed the number.

**Email verification is a link, not a code** — modelled on the password-reset
token (hashed, single-use, `EmailVerificationToken.Consume`), because there is
nowhere on the confirmation screen to type six digits back in. It lives 24
hours rather than the SMS code's two minutes: nothing about the link grants
access to anything, so there is no reason to rush it. Confirming checks the
token's recorded address still matches the account's current email — a link
sent, then the address changed again before it was opened, must not silently
verify the newer value.

Changing either value resets its own flag: a stamp on an address nobody
confirmed is worse than none. Changing the email also fires a fresh
verification link inline (`ICustomerMailer` never throws to its caller, so
this is safe on the request path); changing the phone waits for the code by
design — see above.

**Both checks are optional, per-channel, and off by default.**
`GET`/`PUT /admin/settings/verification` stores two independent booleans the
same way every other settings screen in this codebase stores its section —
`SettingEntry(Section = "verification")`. Turning a toggle on today only
changes what that screen reports back: nothing yet reads either flag to block
a sign-in or a checkout. That enforcement is deliberately left for later,
once there is a real answer for what an unverified shopper should be allowed
to do in the meantime.

## The support mailbox

The address customers write to, read and answered from the panel. IMAP for
receiving, SMTP for replying — `MailboxService`, over MailKit.

**Nothing is stored locally.** The mail server is the record; this reads it on
demand and writes replies back into it, so the panel is one more client of the
same mailbox rather than a copy that can fall out of step with what an operator
sees in their own mail app. UIDs throughout, never sequence numbers: a sequence
number shifts the moment anything else touches the folder.

**Grouped into conversations.** INBOX and Sent are scanned together and grouped
by the outside party plus the normalised subject, so a back-and-forth reads as
one thread and four unrelated topics from one customer read as four. The id is
derived from that group key rather than stored, so opening a thread re-derives
it from a fresh scan with no state to keep in step. `MailSubject.Normalize`
strips any *run* of reply prefixes — a message that has been round twice
carries two — and it is tested against the words that merely start like one
("Return", "Refund"), because stripping those would merge unrelated threads.

A reply carries the original's `Message-Id` in `In-Reply-To` and `References`,
so the customer's client keeps the exchange together, and it is appended to
Sent afterwards. SMTP delivers but does not file: without that step the reply
would reach the customer and vanish from the shop's own record, and an operator
would have no way to tell an answered thread from an unanswered one. Failing to
file is logged rather than reported as a send failure — the mail has already
gone, and offering a retry would send it twice.

The threading scan reads several hundred headers from two folders, and the
list, the search and the paging all need the same one, so it is cached for
twenty seconds and dropped outright by anything that changes state.

### The dangerous part

An inbound body is text written by anyone who knows the published support
address, and the person about to open it holds the highest-privileged session
in the shop. Two independent layers, neither trusted to be enough alone:

1. `MailHtmlSanitizer` — an allow-list built from empty rather than the
   library's defaults trimmed, so a future version cannot widen it by changing
   its own. `img` is absent from the tags and `src`/`background` from the
   attributes, and `background-image` is absent from the CSS properties, so a
   remote image cannot survive by any route. That is deliberate: a remote image
   in an email is a read receipt and an IP leak to whoever sent it, and the
   caller is told it happened so the screen can say images were blocked.
2. The panel renders the result in `<iframe sandbox>` with **neither**
   `allow-scripts` nor `allow-same-origin`, so a bypass of the first layer
   lands in an opaque origin that cannot execute anything or reach the panel.

Attachments are served `application/octet-stream` with `nosniff` and a
`Content-Disposition` of attachment, never the type the sender declared —
honouring that is how an "image" gets rendered as HTML on the panel's origin.
The stored file name is stripped of path separators and control characters.

### Access and the credential

Reading and replying sit behind the **support** section; the settings sit
behind **owner**. Answering customers and holding the credential to the mail
account are different levels of trust.

The password is the one secret in this codebase that is encrypted rather than
hashed, because it has to be replayed to an IMAP server on every connection.
`IDataProtection` holds the key ring outside the database, so a dumped table
alone does not yield it. It never travels outwards: `MailboxSettingsDto` has no
field for it, so there is no route by which the panel could render it, and
`hasPassword` is how the form knows to say "saved" over an empty box. An absent
password on save means "keep the stored one" — which is what an empty field on
a form that never shows it has to mean.

## Serving what was uploaded

`LocalFileStorage` writes into `Storage:RootPath` and hands back URLs under
`Storage:PublicBaseUrl`. Nothing answered those URLs until the invoice stamp
needed to be *displayed* rather than merely stored — every upload in the
product was write-only, and a product photo, a top-up receipt and a stamp all
saved successfully and then 404'd.

`Program.cs` now serves that directory at `PublicBaseUrl`, and only when it is
a path rather than an absolute URL: an absolute one means a CDN or a reverse
proxy is serving the files and this process should not also be.
`ServeUnknownFileTypes` is off, so the type restriction the write path enforces
by sniffing magic bytes holds on the way back out too.

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
| `Storage:RootPath` / `Storage:PublicBaseUrl` | Both | Where uploads land and the URL they are served under. A `PublicBaseUrl` that is a path is served by this process; an absolute URL means a CDN is doing it and this process does not. |
| `Payment:GatewayUrl` | Production | Empty means the sandbox gateway is in use — see below. |
| `Email:Site` | Both | The storefront's origin, for the links in a transactional email. Configured rather than derived from the request, because mail is composed on a worker where there is no request. |

The support mailbox is **not** in this table: it is configured from the panel
rather than from a file, because its password is entered by whoever owns the
account and is encrypted at rest. See the mailbox section above.

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
