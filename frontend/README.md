<div align="center">

# 🪶 Bojan Store — Frontend

**Next.js monorepo: Persian RTL storefront and admin panel, built from a shared design system.**

Next.js 15 · React 19 · TypeScript 5.7 · Tailwind CSS 3.4 · pnpm workspaces

</div>

---

## Overview

Two applications and two shared packages in one pnpm workspace:

```
frontend/
├── apps/
│   ├── storefront/     Customer-facing shop        → localhost:3000
│   └── admin/          Back office                 → localhost:3001
└── packages/
    ├── config/         Tailwind preset, design tokens, shared stylesheet
    └── ui/             Shared components, Persian formatters
```

Both applications consume the same preset and the same component library, so the storefront and the back office cannot drift apart visually.

---

## 🚀 Getting Started

### Prerequisites

- Node.js 22 (20.19 also works — see `engines`)
- pnpm 9 — `corepack enable pnpm`

### Install and run

```bash
pnpm install
```

```bash
cp apps/storefront/.env.example apps/storefront/.env.local
```

```bash
pnpm dev
```

The admin panel runs separately:

```bash
pnpm dev:admin
```

### Checks

```bash
pnpm typecheck
```

```bash
pnpm test
```

```bash
pnpm build
```

---

## 🧪 Tests

Vitest with jsdom and React Testing Library, run from the workspace root so one
command covers every package.

```bash
pnpm test
```

```bash
pnpm test:watch
```

```bash
pnpm test:coverage
```

### What is covered, and what is not

Tests target logic that can silently go wrong — formatting, query mapping,
filtering, derived money, and the interactive components. Pages are **not** unit
tested: they are thin compositions, and the production build plus a route sweep
already prove they render.

| Area | File | Why it matters |
|------|------|----------------|
| Persian formatters | `packages/ui/src/lib/format.test.ts` | Digit transliteration, Jalali dates, the ASCII thousands separator the design requires |
| Class merging | `packages/ui/src/lib/cn.test.ts` | A caller's `className` must beat a component default, including on custom scales |
| URL → query | `apps/storefront/src/lib/search-params.test.ts` | Absent keys must stay absent, or they leak into the request URL |
| Catalogue | `apps/storefront/src/lib/api/catalog.test.ts` | Filtering, sorting, pagination, and that sorting does not mutate the source |
| Account / B2B | `apps/storefront/src/lib/api/account.test.ts`, `business.test.ts` | Lookups by id *and* by the human-facing code |
| Derived money | `apps/storefront/src/lib/mock/derived.test.ts` | Cart, order and quote totals must agree with their own line items |
| Components | `QuantityStepper`, `Price`, `Rating` | Clamping, discount rendering, accessible names |

> The `cn` tests exist because they caught a real bug: `tailwind-merge` did not
> know the design system's custom scales, so `cn('p-md', 'p-lg')` kept **both**
> classes and a caller's override silently lost. `cn.ts` now extends
> `tailwind-merge` with those scales — keep those lists in step with the preset.

---

## 🎨 Design System

Every token comes from the source design and is generated into `packages/config/src/tailwind-preset.ts`. **Do not hand-edit colour values there** — it is the single source of truth for both applications.

### Palette

| Token | Value | Used for |
|-------|-------|----------|
| `primary` | `#003441` | Deep teal — headings, primary text |
| `primary-container` | `#0f4c5c` | Wordmark, panel headers |
| `secondary` | `#a8382b` | Links, savings figures |
| `secondary-container` | `#fe7765` | Coral CTA — the main call to action |
| `coral` | `#F36F5D` | Brand coral used in the design's inline styles |
| `warm-paper` | `#FFF8F1` | Card surface |
| `paper-border` | `#E5E0DA` | Card border |
| `soft-mint` | `#DDF3EF` | Accent surface, avatars, search field |
| `background` / `paper-bg` | `#f9f9f6` / `#FAFAF7` | Page background |

### Type scale

| Token | Size / line-height | Weight |
|-------|--------------------|--------|
| `headline-xl` | 40 / 48 | 700 |
| `headline-lg` | 32 / 40 | 600 |
| `headline-lg-mobile` | 28 / 36 | 600 |
| `display-md` | 24 / 32 | 500 |
| `body-lg` | 18 / 28 | 400 |
| `body-md` | 16 / 24 | 400 |
| `label-md` | 14 / 20 | 600 |
| `caption` | 12 / 16 | 400 |

### Spacing

`xs` 4 · `sm` 8 · `gutter` / `md` 16 · `lg` 24 · `xl` 40 · `margin-mobile` 20 · `margin-desktop` 64

### Shared utilities

Defined once in `packages/config/src/styles.css`, lifted from the design's per-screen inline styles:

| Class | Purpose |
|-------|---------|
| `paper-card` | Warm bordered surface used by most content blocks |
| `glass-nav` / `glass-header` | Translucent blurred app bars |
| `glass-panel` | Blurred overlay panel |
| `hide-scrollbar` | Horizontal carousels without a visible bar |
| `pb-safe` | Clears the iOS home indicator |

---

## 📱 Responsive Approach

Each of the design's 160 screens exists in a mobile and a desktop drawing. In code they are **one component**: mobile is the base state, desktop is layered on at `md:` / `lg:`.

```tsx
// Home hero — 400px bottom-aligned on mobile, 600px centred on desktop
<section className="h-[400px] items-end p-lg md:h-[600px] md:items-center md:p-xl">
```

Breakpoints follow Tailwind defaults: `md` at 768px, `lg` at 1024px. The header switches from a 56px mobile bar to an 80px desktop bar at `md`, and the five-tab bottom navigation is hidden from `md` up.

---

## 🔌 Connecting the .NET Backend

All data access flows through `apps/storefront/src/lib/api/`:

| File | Responsibility |
|------|----------------|
| `client.ts` | Typed `fetch` wrapper, `ApiError` with status, Next.js cache tags |
| `types.ts` | Contracts shared with the backend DTOs |
| `catalog.ts` | Each function calls the real endpoint or falls back to mock data |

While the backend is not running, keep `NEXT_PUBLIC_USE_MOCK_DATA=true`. Once the API is reachable, set it to `false` — **no page needs to change**, because no page imports mock data directly.

> If the backend publishes an OpenAPI document, generate `types.ts` from it rather than maintaining it by hand.

---

## 📄 Screens

All 160 of the design's screens have a route: 89 page routes in the storefront
and 96 in the panel. The counts do not match the design's 90/70 split because a
route is not always a screen — a detail page and its `[slug]` are one drawing,
and several screens the design shows as one flow are separate routes here (the
invoice document, the mailbox thread, the settings sub-pages).

This section used to list the first ten by hand and say the rest were "not
implemented yet". That was true for about a week and quietly wrong for the rest
of the project, which is the trouble with a table somebody has to remember to
update. The routes are the list now: `apps/*/src/app` mirrors it exactly, and
`lib/links.test.ts` walks every internal link in the panel and fails if one
points at a route that does not exist.

## 🌍 Right-to-Left Conventions

- `dir="rtl"` is set on `<html>` and reinforced in the shared stylesheet.
- **Use logical properties for directional spacing** — `ps-*`, `pe-*`, `ms-*`, `me-*`, `start-*`, `end-*`. Never `pl/pr` or `left/right`; they do not mirror.
- **Route every user-visible number through `@bojan/ui`** — `toPersianDigits`, `formatPrice`, `formatNumber`, `formatDate`, `formatDateTime`, `formatPhone`. The design groups thousands with an ASCII comma, which `Intl.NumberFormat('fa-IR')` does not produce, so this is not optional.
- Form input is normalised with `normalizeDigitsInput` before validation, so Persian numerals typed by a user are accepted.
- **Code comments and documentation are English.** Only user-facing strings are Persian.

---

## 🧱 Notable Implementation Details

- **Fonts** — Plus Jakarta Sans and Be Vietnam Pro carry no Arabic-script glyphs, so Vazirmatn sits behind them in the stack. Latin renders in the designed face and Persian falls through automatically. All three load through `next/font/google`.
- **PostCSS ordering** — `postcss-import` must run before Tailwind, otherwise the shared stylesheet's `@layer` and `@apply` rules are never processed. The apps import it by relative path, because `postcss-import` resolves on disk and ignores package `exports` maps.
- **Tailwind preset typing** — the preset is annotated `Partial<Config>` rather than using `satisfies`, so `fontSize` entries infer as tuples instead of arrays.
- **Filter state in the URL** — listings stay server-rendered and shareable; the toolbar is the only client component involved.

---

## ⚠️ Before Production

1. **Icons load from Google's CDN.** Subset and self-host Material Symbols — the CDN is not dependable for users in Iran.
2. **Mock imagery is hosted on `lh3.googleusercontent.com`.** Point `remotePatterns` in `next.config.mjs` at the real media host once it exists.
3. **Authentication is UI only.** `LoginForm` has no session handling yet.
4. **The cart is client-side.** It needs to be wired to the `/cart` endpoints.

---

## 📜 License

Proprietary — © Bojan Store. All rights reserved.
