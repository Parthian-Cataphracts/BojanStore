import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeAll } from 'vitest';

beforeAll(() => {
  // The formatters rely on the Persian calendar and Persian locale data.
  // If the runtime shipped a trimmed ICU, date assertions would silently drift,
  // so fail loudly here instead of producing confusing diffs later.
  const persian = new Intl.DateTimeFormat('fa-IR-u-ca-persian', { year: 'numeric' });
  if (persian.resolvedOptions().calendar !== 'persian') {
    throw new Error(
      'Node was built without full ICU: the Persian calendar is unavailable, so date tests cannot run.',
    );
  }
});

/*
  jsdom has no ResizeObserver, and `StickyActionBar` builds one to publish its
  own height. Any component rendered inside that bar — the product page's
  add-to-cart control among them — throws on mount without this.

  A stub rather than a polyfill: nothing under test asserts on a measurement,
  and a real implementation in a DOM with no layout would only ever report
  zeroes anyway.
*/
if (!('ResizeObserver' in globalThis)) {
  globalThis.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
}

afterEach(() => {
  cleanup();
});
