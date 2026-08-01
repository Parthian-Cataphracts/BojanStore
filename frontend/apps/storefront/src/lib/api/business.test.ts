import { describe, expect, it } from 'vitest';
import { getB2BRequest, getGiftBundles, getQuote } from './business';
import { giftBundleCategories, mockGiftBundles } from '../mock/business';

describe('getGiftBundles', () => {
  it('returns everything for the "all" chip', async () => {
    const bundles = await getGiftBundles(giftBundleCategories[0]);
    expect(bundles).toHaveLength(mockGiftBundles.length);
  });

  it('returns everything when no category is given', async () => {
    expect(await getGiftBundles()).toHaveLength(mockGiftBundles.length);
  });

  it('filters by category', async () => {
    const artistic = await getGiftBundles('هنری');
    expect(artistic.length).toBeGreaterThan(0);
    expect(artistic.every((bundle) => bundle.category === 'هنری')).toBe(true);
  });

  it('quotes a minimum quantity above one on every bundle', async () => {
    // Corporate bundles are not single-unit purchases; a minimum of 1 would be
    // a data error that the storefront would happily render.
    const bundles = await getGiftBundles();
    expect(bundles.every((bundle) => bundle.minimumQuantity > 1)).toBe(true);
  });
});

describe('getB2BRequest', () => {
  it('finds a request by id and by code', async () => {
    expect((await getB2BRequest('req-1'))?.id).toBe('req-1');
    expect((await getB2BRequest('B2B-8902'))?.id).toBe('req-1');
  });

  it('returns null for an unknown reference', async () => {
    expect(await getB2BRequest('B2B-0000')).toBeNull();
  });

  it('points every quoted request at a quote that exists', async () => {
    const request = await getB2BRequest('req-1');
    expect(request?.status).toBe('quoted');
    expect(request?.quoteId).toBeDefined();
    expect(await getQuote(request!.quoteId!)).not.toBeNull();
  });
});

describe('getQuote', () => {
  it('finds a quote by id and by number', async () => {
    expect((await getQuote('qt-1'))?.id).toBe('qt-1');
    expect((await getQuote('QT-1405-8942'))?.id).toBe('qt-1');
  });

  it('returns null for an unknown reference', async () => {
    expect(await getQuote('QT-0000')).toBeNull();
  });
});
