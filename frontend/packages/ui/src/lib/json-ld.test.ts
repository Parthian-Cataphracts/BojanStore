import { describe, expect, it } from 'vitest';
import { serializeJsonLd } from './json-ld';

describe('serializeJsonLd', () => {
  it('escapes a closing script tag so it cannot break out of the block', () => {
    const output = serializeJsonLd({ name: 'دفتر </script><script>alert(1)</script>' });

    expect(output).not.toContain('</script>');
    expect(output).not.toContain('<');
    expect(output).not.toContain('>');
  });

  it('escapes ampersands', () => {
    expect(serializeJsonLd({ brand: 'A & B' })).not.toContain('&');
  });

  it('escapes the line separators that are legal in JSON but not in JS strings', () => {
    const output = serializeJsonLd({ body: 'a b c' });

    expect(output).toContain('\\u2028');
    expect(output).toContain('\\u2029');
  });

  it('still parses back to the original value', () => {
    const payload = {
      '@context': 'https://schema.org',
      name: 'خودکار <b>ویژه</b> & دفتر',
      price: 120_000,
    };

    expect(JSON.parse(serializeJsonLd(payload))).toEqual(payload);
  });

  it('leaves Persian text untouched', () => {
    expect(JSON.parse(serializeJsonLd({ title: 'دفتر پلنر روزانه' }))).toEqual({
      title: 'دفتر پلنر روزانه',
    });
  });
});
