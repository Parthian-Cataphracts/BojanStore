// @vitest-environment node

/**
 * What this server tells the API about who it is calling for.
 *
 * Node rather than jsdom, and that is the point of the file rather than a
 * detail of it: the header is only ever sent when there is no `window`, so a
 * jsdom test would assert the "in a browser" branch while reading like it
 * asserted the other one.
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const requestHeaders = vi.hoisted(() => ({ current: new Headers() }));
const scoped = vi.hoisted(() => ({ current: true }));

vi.mock('next/headers', () => ({
  headers: async () => {
    if (!scoped.current) {
      // What Next throws when there is no request to read — a build-time
      // render, or a background job.
      throw new Error('`headers` was called outside a request scope.');
    }
    return requestHeaders.current;
  },
}));

let sent: Headers;

beforeEach(async () => {
  process.env.API_BASE_URL = 'http://api.test/api';
  process.env.API_KEY = 'server-side-only';
  scoped.current = true;
  requestHeaders.current = new Headers({ 'x-forwarded-for': '203.0.113.9, 198.51.100.4' });

  vi.stubGlobal('fetch', (_url: unknown, init: RequestInit) => {
    sent = new Headers(init.headers as HeadersInit);
    return Promise.resolve(
      new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    );
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.resetModules();
  delete process.env.API_BASE_URL;
  delete process.env.API_KEY;
});

async function client() {
  // Imported per test: the module reads the base URL and the key at load.
  return (await import('./client')).api;
}

describe('the address forwarded to the API', () => {
  it('names the shopper on a write, not this server', async () => {
    // The whole point. Without it every shopper shares one partition of the
    // API's per-address limits, because every one of these calls leaves from
    // the same container: five sign-in codes a minute for the entire shop.
    const api = await client();
    await api.post('/auth/otp/request', { phone: '09120000000' });

    expect(sent.get('X-Forwarded-For')).toBe('198.51.100.4');
  });

  it('sends the trusted end of the chain, never what the caller wrote', async () => {
    requestHeaders.current = new Headers({
      'x-forwarded-for': '9.9.9.9, 198.51.100.4',
    });

    const api = await client();
    await api.post('/orders', {});

    // Passing the left-hand entry on would hand every caller a fresh window,
    // which is the bug this header exists to close rather than to reopen.
    expect(sent.get('X-Forwarded-For')).toBe('198.51.100.4');
  });

  it('stays off a plain GET, so a statically rendered page keeps its rendering', async () => {
    // Reading `headers()` inside a static render opts the page out of it. The
    // API exempts this server from the read limits for exactly that reason, so
    // there is nothing lost by staying quiet here.
    const api = await client();
    await api.get('/products');

    expect(sent.has('X-Forwarded-For')).toBe(false);
  });

  it('is sent on the reads that ask for it', async () => {
    // Order tracking and the chat poll: GETs the API limits per shopper, and
    // both reached only from a route handler, which is never static.
    const api = await client();
    await api.get('/orders/track', { forwardClient: true });

    expect(sent.get('X-Forwarded-For')).toBe('198.51.100.4');
  });

  it('can be suppressed on a write that has no shopper behind it', async () => {
    const api = await client();
    await api.post('/internal/thing', {}, { forwardClient: false });

    expect(sent.has('X-Forwarded-For')).toBe(false);
  });

  it('sends no header rather than a placeholder when nothing names an address', async () => {
    requestHeaders.current = new Headers();

    const api = await client();
    await api.post('/auth/otp/request', { phone: '09120000000' });

    // A header reading "unknown" would be a partition key of its own, and one
    // every unattributable caller would share with every other.
    expect(sent.has('X-Forwarded-For')).toBe(false);
  });

  it('still makes the call when there is no request to read', async () => {
    // A build-time render or a background job. The call goes as this server and
    // is limited as one, which is the old behaviour and the right fallback.
    scoped.current = false;

    const api = await client();
    await expect(api.post('/auth/otp/request', { phone: '09120000000' })).resolves.toBeTruthy();

    expect(sent.has('X-Forwarded-For')).toBe(false);
    // The call is still identified as coming from this server.
    expect(sent.get('X-Api-Key')).toBe('server-side-only');
  });
});
