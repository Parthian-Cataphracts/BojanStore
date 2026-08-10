import { describe, expect, it } from 'vitest';
import { safeNextPath } from './safe-next';

const FALLBACK = '/account';

describe('safeNextPath', () => {
  it('keeps a same-origin path, with its query and hash', () => {
    expect(safeNextPath('/checkout/review', FALLBACK)).toBe('/checkout/review');
    expect(safeNextPath('/search?q=دفتر&page=2', FALLBACK)).toBe('/search?q=%D8%AF%D9%81%D8%AA%D8%B1&page=2');
    expect(safeNextPath('/magazine/post#comments', FALLBACK)).toBe('/magazine/post#comments');
  });

  it('falls back when there is nothing to redirect to', () => {
    expect(safeNextPath(null, FALLBACK)).toBe(FALLBACK);
    expect(safeNextPath(undefined, FALLBACK)).toBe(FALLBACK);
    expect(safeNextPath('', FALLBACK)).toBe(FALLBACK);
  });

  it('refuses anything that names another origin', () => {
    expect(safeNextPath('https://evil.example', FALLBACK)).toBe(FALLBACK);
    expect(safeNextPath('//evil.example', FALLBACK)).toBe(FALLBACK);
    expect(safeNextPath('/\\evil.example', FALLBACK)).toBe(FALLBACK);
    expect(safeNextPath('javascript:alert(1)', FALLBACK)).toBe(FALLBACK);
    expect(safeNextPath('checkout', FALLBACK)).toBe(FALLBACK);
  });

  /**
   * The parser removes these three characters before reading the URL, so the
   * browser sees `//evil.example` where a prefix test saw a single slash.
   */
  it.each(['\t', '\n', '\r'])(
    'refuses a protocol-relative URL hidden behind %j, which URL parsing strips',
    (stripped) => {
      expect(safeNextPath(`/${stripped}/evil.example`, FALLBACK)).toBe(FALLBACK);
      expect(safeNextPath(`/${stripped}\\evil.example`, FALLBACK)).toBe(FALLBACK);
      expect(safeNextPath(`${stripped}//evil.example`, FALLBACK)).toBe(FALLBACK);
    },
  );

  it('refuses a scheme split by a stripped character', () => {
    expect(safeNextPath('java\tscript:alert(1)', FALLBACK)).toBe(FALLBACK);
  });
});
