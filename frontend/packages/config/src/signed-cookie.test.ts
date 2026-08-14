import { describe, expect, it } from 'vitest';
import { createSignedCookieCodec, isExpired, sha256Hex } from './signed-cookie';

const secret = (value: string) => () => new TextEncoder().encode(value);

const codec = createSignedCookieCodec(secret('a-secret-long-enough-to-be-real'));
const other = createSignedCookieCodec(secret('a-different-secret-entirely-ok'));

describe('createSignedCookieCodec', () => {
  it('reads back what it signed', async () => {
    const payload = { sub: 'c-1', phone: '09120000000', exp: 1_800_000_000 };
    expect(await codec.verify(await codec.sign(payload))).toEqual(payload);
  });

  it('survives a payload with non-ASCII in it', async () => {
    // Names are Persian here, and a base64 helper that walked bytes as
    // characters would corrupt exactly this.
    const payload = { name: 'بوژان استور', role: 'مالک' };
    expect(await codec.verify(await codec.sign(payload))).toEqual(payload);
  });

  it('refuses a token signed with another secret', async () => {
    // The panel and the storefront sign with deliberately different secrets, so
    // this is what stops a customer cookie being replayed as an operator one.
    expect(await codec.verify(await other.sign({ sub: 'c-1' }))).toBeNull();
  });

  it('refuses a payload edited after signing', async () => {
    const token = await codec.sign({ sub: 'c-1', role: 'customer' });
    const forged = await other.sign({ sub: 'c-1', role: 'owner' });

    // The forged body carried over onto the genuine signature.
    expect(await codec.verify(`${forged.split('.')[0]}.${token.split('.')[1]}`)).toBeNull();
  });

  it('refuses anything that is not a token', async () => {
    expect(await codec.verify(undefined)).toBeNull();
    expect(await codec.verify('')).toBeNull();
    expect(await codec.verify('no-dot-at-all')).toBeNull();
    expect(await codec.verify('.leading-dot')).toBeNull();
    expect(await codec.verify('not-base64.$$$')).toBeNull();
  });

  it('signs the payload as given and leaves expiry to the caller', async () => {
    // The codec has no clock of its own on purpose: the two apps expire their
    // sessions at very different ages, and both put `exp` in themselves.
    const signed = await codec.verify<{ exp?: number }>(await codec.sign({ sub: 'c-1' }));
    expect(signed).toEqual({ sub: 'c-1' });
  });
});

describe('isExpired', () => {
  it('reads unix seconds, and treats anything else as expired', () => {
    expect(isExpired(Math.floor(Date.now() / 1000) + 60)).toBe(false);
    expect(isExpired(Math.floor(Date.now() / 1000) - 60)).toBe(true);
    expect(isExpired(undefined)).toBe(true);
    expect(isExpired('1800000000')).toBe(true);
  });
});

describe('sha256Hex', () => {
  it('matches the published digest for a known input', async () => {
    expect(await sha256Hex('abc')).toBe(
      'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad',
    );
  });

  it('pads every byte to two characters', async () => {
    expect(await sha256Hex('bojan')).toMatch(/^[0-9a-f]{64}$/);
  });
});
