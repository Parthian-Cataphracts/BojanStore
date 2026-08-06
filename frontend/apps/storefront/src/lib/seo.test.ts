import { describe, expect, it } from 'vitest';
import { RIAL_PER_TOMAN, absoluteUrl, siteUrl, toRial } from './seo';

describe('toRial', () => {
  /**
   * The product page declared `priceCurrency: 'IRR'` against a Toman figure,
   * which advertised every product to shopping engines at a tenth of its price.
   */
  it('converts Toman to the Rial figure IRR means', () => {
    expect(toRial(850_000)).toBe(8_500_000);
    expect(RIAL_PER_TOMAN).toBe(10);
  });

  it('leaves zero alone', () => {
    expect(toRial(0)).toBe(0);
  });
});

describe('absoluteUrl', () => {
  it('carries the origin, which JSON-LD does not resolve on its own', () => {
    expect(absoluteUrl('/products/p-01')).toBe(`${siteUrl}/products/p-01`);
  });

  it('tolerates a path given without its leading slash', () => {
    expect(absoluteUrl('products/p-01')).toBe(`${siteUrl}/products/p-01`);
  });

  it('never doubles the slash between origin and path', () => {
    expect(absoluteUrl('/')).not.toContain('//products');
    expect(siteUrl.endsWith('/')).toBe(false);
  });
});
