import { afterEach, describe, expect, it } from 'vitest';
import { isSameOriginRequest } from './origin';

function request(headers: Record<string, string>): Request {
  return new Request('https://bojan.test/api/orders', { method: 'POST', headers });
}

afterEach(() => {
  delete process.env.TRUSTED_ORIGINS;
});

describe('isSameOriginRequest', () => {
  it('accepts the browser saying the request came from this site', () => {
    expect(isSameOriginRequest(request({ 'sec-fetch-site': 'same-origin' }))).toBe(true);
  });

  it('refuses the browser saying it came from somewhere else', () => {
    expect(
      isSameOriginRequest(
        request({ 'sec-fetch-site': 'cross-site', origin: 'https://evil.example' }),
      ),
    ).toBe(false);

    // A subdomain is a different origin, and `same-site` is the header that
    // says so — accepting it would let anything under the registrable domain
    // spend a session.
    expect(isSameOriginRequest(request({ 'sec-fetch-site': 'same-site' }))).toBe(false);

    // Typing the URL cannot produce a POST to an API route, so `none` is not a
    // shape this app makes.
    expect(isSameOriginRequest(request({ 'sec-fetch-site': 'none' }))).toBe(false);
  });

  it('falls back to Origin against the host it was addressed to', () => {
    expect(isSameOriginRequest(request({ origin: 'https://bojan.test', host: 'bojan.test' }))).toBe(true);
    expect(isSameOriginRequest(request({ origin: 'https://evil.example', host: 'bojan.test' }))).toBe(false);
  });

  it('reads the host from the proxy when there is one', () => {
    expect(
      isSameOriginRequest(
        request({ origin: 'https://bojan.test', host: 'localhost:3000', 'x-forwarded-host': 'bojan.test' }),
      ),
    ).toBe(true);
  });

  it('refuses a request that says nothing about where it came from', () => {
    // Not a browser form post, and these routes serve nothing else.
    expect(isSameOriginRequest(request({ host: 'bojan.test' }))).toBe(false);
  });

  it('honours a configured second origin', () => {
    process.env.TRUSTED_ORIGINS = 'https://partner.example, https://other.example';

    expect(
      isSameOriginRequest(
        request({ 'sec-fetch-site': 'cross-site', origin: 'https://partner.example' }),
      ),
    ).toBe(true);
    expect(
      isSameOriginRequest(
        request({ 'sec-fetch-site': 'cross-site', origin: 'https://evil.example' }),
      ),
    ).toBe(false);
  });

  it('does not mistake a malformed Origin for a match', () => {
    expect(isSameOriginRequest(request({ origin: 'null', host: 'bojan.test' }))).toBe(false);
    expect(isSameOriginRequest(request({ origin: 'bojan.test', host: 'bojan.test' }))).toBe(false);
  });
});
