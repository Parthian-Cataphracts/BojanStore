import { afterEach, describe, expect, it } from 'vitest';
import { clientAddress } from './client-address';

function headers(values: Record<string, string>): Headers {
  return new Headers(values);
}

afterEach(() => {
  delete process.env.TRUSTED_PROXY_HOPS;
});

describe('clientAddress', () => {
  it('takes the entry the nearest proxy wrote, not the one the client sent', () => {
    // nginx appends, so the real client sits at the right-hand end. Reading the
    // left-hand end is the bug this whole module exists to avoid: a caller who
    // varies it buys a fresh rate-limit window on every request.
    expect(clientAddress(headers({ 'x-forwarded-for': '203.0.113.9, 198.51.100.4' }))).toBe(
      '198.51.100.4',
    );
  });

  it('ignores a spoofed chain entirely when only one proxy stands in front', () => {
    expect(
      clientAddress(headers({ 'x-forwarded-for': 'not-an-address, 9.9.9.9, 198.51.100.4' })),
    ).toBe('198.51.100.4');
  });

  it('counts back further when more proxies are declared', () => {
    process.env.TRUSTED_PROXY_HOPS = '2';
    // A CDN in front of nginx: the CDN wrote the client, nginx then appended
    // the CDN. Two back is the client.
    expect(clientAddress(headers({ 'x-forwarded-for': '198.51.100.4, 203.0.113.9' }))).toBe(
      '198.51.100.4',
    );
  });

  it('refuses to guess when the chain is shorter than the declared hops', () => {
    process.env.TRUSTED_PROXY_HOPS = '3';
    // Fewer entries than proxies means nothing in the chain was written by the
    // proxy we trust, so every entry is the caller's own invention.
    expect(clientAddress(headers({ 'x-forwarded-for': '203.0.113.9' }))).toBeNull();
  });

  it('tolerates whitespace and empty entries', () => {
    expect(clientAddress(headers({ 'x-forwarded-for': ' , 203.0.113.9 ,  198.51.100.4 ' }))).toBe(
      '198.51.100.4',
    );
  });

  it('falls back to x-real-ip when there is no chain', () => {
    expect(clientAddress(headers({ 'x-real-ip': ' 198.51.100.4 ' }))).toBe('198.51.100.4');
  });

  it('is null when nothing names an address', () => {
    // Null rather than a placeholder: the limiter turns this into one shared
    // bucket, and the API client sends no header at all.
    expect(clientAddress(headers({}))).toBeNull();
  });

  it('treats an unparseable hop count as one', () => {
    process.env.TRUSTED_PROXY_HOPS = 'nonsense';
    expect(clientAddress(headers({ 'x-forwarded-for': '203.0.113.9, 198.51.100.4' }))).toBe(
      '198.51.100.4',
    );
  });

  it('never counts fewer than one hop', () => {
    // Zero would index past the end of the chain and read undefined; a negative
    // value would walk forwards into what the client supplied.
    process.env.TRUSTED_PROXY_HOPS = '0';
    expect(clientAddress(headers({ 'x-forwarded-for': '203.0.113.9, 198.51.100.4' }))).toBe(
      '198.51.100.4',
    );
  });
});
