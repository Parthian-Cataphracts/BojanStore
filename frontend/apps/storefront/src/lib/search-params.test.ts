import { describe, expect, it } from 'vitest';
import { first, toProductQuery } from './search-params';

describe('first', () => {
  it('returns a lone string unchanged', () => {
    expect(first('newest')).toBe('newest');
  });

  it('takes the first entry when a param repeats', () => {
    expect(first(['a', 'b'])).toBe('a');
  });

  it('passes undefined through', () => {
    expect(first(undefined)).toBeUndefined();
  });
});

describe('toProductQuery', () => {
  it('produces an empty query when nothing is set', () => {
    expect(toProductQuery({})).toEqual({});
  });

  it('omits absent keys entirely rather than setting them undefined', () => {
    // A key present with an undefined value would be serialised into the
    // request URL by the API client, so absence has to be real absence.
    const query = toProductQuery({ category: 'art-tools' });
    expect(query).toEqual({ category: 'art-tools' });
    expect(Object.keys(query)).toEqual(['category']);
  });

  it('maps the q param onto search, which is what the API calls it', () => {
    expect(toProductQuery({ q: 'آبرنگ' })).toEqual({ search: 'آبرنگ' });
  });

  it('coerces numeric params to numbers', () => {
    const query = toProductQuery({ minPrice: '100000', maxPrice: '500000', page: '3' });
    expect(query.minPrice).toBe(100_000);
    expect(query.maxPrice).toBe(500_000);
    expect(query.page).toBe(3);
  });

  it('treats inStock as a flag only when it is exactly "true"', () => {
    expect(toProductQuery({ inStock: 'true' }).inStockOnly).toBe(true);
    expect(toProductQuery({ inStock: 'false' }).inStockOnly).toBeUndefined();
    expect(toProductQuery({ inStock: '1' }).inStockOnly).toBeUndefined();
  });

  it('carries sort through untouched', () => {
    expect(toProductQuery({ sort: 'price-asc' }).sort).toBe('price-asc');
  });

  it('reads the first value when a param is repeated in the URL', () => {
    expect(toProductQuery({ category: ['notebooks', 'art-tools'] }).category).toBe('notebooks');
  });
});
