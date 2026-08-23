import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

/**
 * Every link the API puts in an email has to be a page this app serves.
 *
 * `EmailLinks` composes those URLs, and its own remarks say why it exists in
 * one file: "Getting one wrong is a dead link in a customer's inbox that
 * nothing in the build would catch." Nothing did catch it. Two of them pointed
 * at `/auth/reset-password` and `/auth/forgot-password`, which this app has
 * never had — the routes are `/reset-password` and `/forgot-password` — so
 * every password-reset email ever sent led to a 404, and a third pointed at a
 * per-ticket page that does not exist.
 *
 * The failure is silent on both sides: the API composed a URL and sent it, the
 * storefront was never asked for it, and the only person who saw anything was a
 * customer who could not get back into their account.
 *
 * This is the check that was missing. It reads the real C# and asserts each
 * path resolves to a real route folder — the same shape as the panel's
 * `permissions.test.ts`, which pins the other list the two sides have to agree
 * on.
 */

const here = path.dirname(fileURLToPath(import.meta.url));
const emailLinks = path.resolve(
  here,
  '../../../../../backend/src/Bojan.Application/Notifications/EmailLinks.cs',
);
const appDir = path.resolve(here, '../app');

/**
 * The route folder a path maps to, with dynamic segments collapsed.
 *
 * `/account/orders/{orderId}` is served by `account/orders/[id]`, and an
 * interpolated C# hole is what marks the segment as dynamic — so a `{...}`
 * segment matches any single `[...]` folder, and a literal one has to match by
 * name.
 */
function resolvesToRoute(routePath: string): boolean {
  const segments = routePath.split('/').filter(Boolean);

  let current = appDir;
  for (const segment of segments) {
    if (segment.startsWith('{')) {
      // A dynamic segment: any `[param]` folder at this level will serve it.
      const dynamic = ['[id]', '[slug]', '[token]'].find((name) =>
        existsSync(path.join(current, name)),
      );
      if (!dynamic) return false;
      current = path.join(current, dynamic);
      continue;
    }

    current = path.join(current, segment);
    if (!existsSync(current)) return false;
  }

  return existsSync(path.join(current, 'page.tsx'));
}

describe('the links the API puts in emails', () => {
  it.runIf(existsSync(emailLinks))('all point at pages this app serves', () => {
    const source = readFileSync(emailLinks, 'utf8');

    // Every `Path("/...")` and `Path($"/...")` in the file, with any query
    // string dropped — the route is the part before the `?`.
    const paths = [...source.matchAll(/Path\(\$?"(\/[^"]*)"/g)]
      .map((match) => match[1]!.split('?')[0]!)
      .filter((value, index, all) => all.indexOf(value) === index);

    // Guards the regex itself: a refactor that renames `Path` would otherwise
    // make this test pass by finding nothing at all.
    expect(paths.length).toBeGreaterThan(5);

    const dead = paths.filter((value) => !resolvesToRoute(value));
    expect(dead, 'paths emailed to customers that this app has no route for').toEqual([]);
  });
});
