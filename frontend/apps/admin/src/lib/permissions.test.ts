import { existsSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { adminNav } from './nav';
import { SECTIONS, assignableScreens, canOpen, canOpenPath, navItemFor } from './permissions';

/**
 * What an operator may open, tested at the level the owner decides it.
 *
 * Three of these are about the same trap from three sides: nine nav entries are
 * a prefix of another one, so anything that resolves a path by "starts with"
 * gets the wrong answer for the longer of the pair. `/settings` is a prefix of
 * `/settings/logs`, which would have handed the server log to whoever was given
 * the shop's settings.
 */
describe('the permission catalogue', () => {
  it('offers every menu entry that has a section, and nothing else', () => {
    const offered = assignableScreens().flatMap((section) => section.screens.map((s) => s.key));
    const expected = adminNav.flatMap((group) =>
      group.items.filter((item) => item.section).map((item) => item.href),
    );

    expect(offered.sort()).toEqual(expected.sort());
  });

  it('leaves the dashboard and the operator’s own account out', () => {
    // Neither is a permission: one is where the panel opens, and the other
    // three are the screens somebody changes their own password on. A grant
    // that could remove them would lock an operator out of their own account.
    const offered = assignableScreens().flatMap((section) => section.screens.map((s) => s.key));

    for (const key of ['/', '/settings/profile', '/settings/password', '/settings/two-factor']) {
      expect(offered).not.toContain(key);
    }
  });

  it('files every screen under a section the checklist draws', () => {
    const known = new Set<string>(SECTIONS.map((section) => section.key));

    for (const item of adminNav.flatMap((group) => group.items)) {
      if (item.section) expect(known.has(item.section)).toBe(true);
    }
  });
});

/**
 * The API keeps the same keys in `PanelScreen.Sections`, because a grant has to
 * mean something to an endpoint as well as to a menu. Two lists that must
 * agree, and only one of them has the labels — so the check lives here.
 *
 * A key this side is missing over there cannot be stored at all: the service
 * drops what it does not recognise, so the checkbox is ticked, saved, and comes
 * back empty. That is a quiet enough failure to be worth a loud test.
 */
describe('the API’s copy of the catalogue', () => {
  const catalogue = path.resolve(
    path.dirname(fileURLToPath(import.meta.url)),
    '../../../../../backend/src/Bojan.Domain/Admin/PanelScreen.cs',
  );

  it.runIf(existsSync(catalogue))('names exactly the screens this menu does', () => {
    const source = readFileSync(catalogue, 'utf8');
    const declared = [...source.matchAll(/\["(\/[^"]*)"\]\s*=/g)].map((match) => match[1]);
    const offered = assignableScreens().flatMap((section) => section.screens.map((s) => s.key));

    expect(declared.sort()).toEqual(offered.sort());
  });
});

describe('canOpen', () => {
  const item = (href: string) =>
    adminNav.flatMap((group) => group.items).find((i) => i.href === href)!;

  it('treats no grants at all as not narrowed', () => {
    // The state an operator is in the moment they are appointed. Reading it as
    // "nothing" would leave every new operator staring at an empty menu.
    expect(canOpen(null, item('/orders'))).toBe(true);
    expect(canOpen([], item('/orders'))).toBe(true);
  });

  it('opens every screen under a granted section', () => {
    expect(canOpen(['orders'], item('/orders'))).toBe(true);
    expect(canOpen(['orders'], item('/returns'))).toBe(true);
    expect(canOpen(['orders'], item('/invoices'))).toBe(true);
  });

  it('opens one screen without the section around it', () => {
    // The whole point of the finer grain: returns and orders are one section,
    // so this was impossible to express before.
    expect(canOpen(['/returns'], item('/returns'))).toBe(true);
    expect(canOpen(['/returns'], item('/orders'))).toBe(false);
    expect(canOpen(['/returns'], item('/invoices'))).toBe(false);
  });

  it('never hides what is nobody’s permission', () => {
    expect(canOpen(['/returns'], item('/'))).toBe(true);
    expect(canOpen(['/returns'], item('/settings/password'))).toBe(true);
  });
});

describe('canOpenPath', () => {
  it('resolves a path to the longest entry that matches it', () => {
    expect(navItemFor('/settings/logs')?.href).toBe('/settings/logs');
    expect(navItemFor('/inventory/low-stock')?.href).toBe('/inventory/low-stock');
    // A detail screen has no entry of its own and belongs to its list.
    expect(navItemFor('/orders/BZ-1042')?.href).toBe('/orders');
  });

  it('does not let a shorter grant cover a longer screen', () => {
    expect(canOpenPath(['/settings'], '/settings/logs')).toBe(false);
    expect(canOpenPath(['/settings'], '/settings')).toBe(true);
    // The section above both still covers both.
    expect(canOpenPath(['settings'], '/settings/logs')).toBe(true);
  });

  it('carries a grant to the detail screens under it', () => {
    expect(canOpenPath(['/orders'], '/orders/BZ-1042')).toBe(true);
    expect(canOpenPath(['/returns'], '/orders/BZ-1042')).toBe(false);
  });

  it('leaves alone what no entry names', () => {
    // `/forbidden` and `/login` are not destinations anybody is granted, and a
    // guard that refused them would refuse the screen it redirects to.
    expect(canOpenPath(['/returns'], '/forbidden')).toBe(true);
  });
});
