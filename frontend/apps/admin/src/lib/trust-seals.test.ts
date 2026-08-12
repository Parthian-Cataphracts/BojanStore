import { describe, expect, it } from 'vitest';
import { parseTrustSeals, type TrustSeal } from './trust-seals';

/**
 * The stored row is the contract between three readers: this form, the
 * storefront's footer, and `StoreStatusQueries.ReadTrustSeals` on the API. The
 * settings table holds it as one JSON string, so the shape is the only thing
 * keeping them agreeing — hence a test on the shape rather than on the form.
 */
describe('parseTrustSeals', () => {
  it('reads the list the form writes', () => {
    const written: TrustSeal[] = [
      { title: 'نماد اعتماد الکترونیکی', subtitle: '۱۲۳۴۵۶۷۸', link: 'https://trustseal.enamad.ir', enabled: true },
      { title: 'ساماندهی', subtitle: '', link: '', enabled: false },
    ];

    expect(parseTrustSeals(JSON.stringify(written))).toEqual(written);
  });

  it('treats a missing row as no marks', () => {
    expect(parseTrustSeals(undefined)).toEqual([]);
    expect(parseTrustSeals('')).toEqual([]);
    expect(parseTrustSeals('   ')).toEqual([]);
  });

  // A settings row can be hand-edited by anyone with database access. Losing
  // the list is survivable; throwing would take down the whole settings screen,
  // including the fields that are fine.
  it('survives a row that is not the list it should be', () => {
    expect(parseTrustSeals('{ not json')).toEqual([]);
    expect(parseTrustSeals('"a string"')).toEqual([]);
    expect(parseTrustSeals('{"title":"one"}')).toEqual([]);
    expect(parseTrustSeals('[null, 3, "x"]')).toEqual([]);
  });

  it('drops a row with no name rather than printing an empty pill', () => {
    const raw = '[{"title":"","subtitle":"x","link":"","enabled":true},{"title":"واقعی","enabled":true}]';

    expect(parseTrustSeals(raw)).toEqual([
      { title: 'واقعی', subtitle: '', link: '', enabled: true },
    ]);
  });

  // A row written before the switch existed is a mark somebody entered to
  // display, so absent reads as shown rather than as hidden.
  it('reads a missing switch as shown', () => {
    expect(parseTrustSeals('[{"title":"قدیمی"}]')).toEqual([
      { title: 'قدیمی', subtitle: '', link: '', enabled: true },
    ]);

    expect(parseTrustSeals('[{"title":"خاموش","enabled":false}]')[0].enabled).toBe(false);
  });

  it('trims what the owner typed', () => {
    expect(parseTrustSeals('[{"title":"  نماد  ","subtitle":" ۱۲ ","link":" https://a.test ","enabled":true}]')).toEqual([
      { title: 'نماد', subtitle: '۱۲', link: 'https://a.test', enabled: true },
    ]);
  });

  it('ignores a non-string field rather than rendering it', () => {
    expect(parseTrustSeals('[{"title":"نماد","subtitle":42,"link":{"a":1},"enabled":true}]')).toEqual([
      { title: 'نماد', subtitle: '', link: '', enabled: true },
    ]);
  });
});
