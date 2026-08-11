/**
 * How the prose an owner typed becomes the shape these screens render.
 *
 * The parsing is the whole risk in making these pages editable: if it drops a
 * paragraph, the shop's returns policy is quietly shorter than the owner wrote
 * and nothing anywhere would say so.
 */

import { describe, expect, it } from 'vitest';

import { getContentPage, pageSlugs, parseContentBody } from './pages';
import type { ContentPageData } from '../content/pages';

const fallback: ContentPageData = {
  title: 'عنوان پیش‌فرض',
  intro: 'مقدمه‌ی پیش‌فرض',
  blocks: [{ title: 'بخش پیش‌فرض', body: ['متن پیش‌فرض'] }],
};

describe('getContentPage', () => {
  /**
   * The tests run in mock mode, which is the same branch a shop takes when it
   * has written nothing — a page with policies rather than an empty one.
   */
  it('keeps the shipped copy when the shop has written nothing', async () => {
    expect(await getContentPage(pageSlugs.terms, fallback)).toEqual(fallback);
  });
});

describe('parseContentBody', () => {
  it('splits a body on ## headings', () => {
    const { intro, blocks } = parseContentBody(
      'خلاصه‌ی صفحه\n\n## ثبت سفارش\n\nمتن اول\n\nمتن دوم\n\n## پرداخت\n\nمتن سوم',
      'قوانین',
    );

    expect(intro).toBe('خلاصه‌ی صفحه');
    expect(blocks).toEqual([
      { title: 'ثبت سفارش', body: ['متن اول', 'متن دوم'] },
      { title: 'پرداخت', body: ['متن سوم'] },
    ]);
  });

  /** A shop that just pastes text gets a readable page, not one long line. */
  it('treats a body with no headings as an intro and a run of paragraphs', () => {
    const { intro, blocks } = parseContentBody('اول\n\nدوم\n\nسوم', 'قوانین');

    expect(intro).toBe('اول');
    expect(blocks).toEqual([{ body: ['دوم', 'سوم'] }]);
  });

  /** One paragraph is an intro and nothing else — not an empty card under it. */
  it('renders a single paragraph as the intro alone', () => {
    expect(parseContentBody('فقط یک خط', 'قوانین')).toEqual({ intro: 'فقط یک خط', blocks: [] });
  });

  /**
   * Paragraphs written above the first heading are not lost. Dropping them
   * would silently shorten a policy the shop is held to.
   */
  it('keeps text written above the first heading', () => {
    const { intro, blocks } = parseContentBody(
      'مقدمه\n\nادامه‌ی مقدمه\n\n## بخش\n\nمتن',
      'قوانین',
    );

    expect(intro).toBe('مقدمه');
    expect(blocks).toEqual([
      { title: 'قوانین', body: ['ادامه‌ی مقدمه'] },
      { title: 'بخش', body: ['متن'] },
    ]);
  });

  /** Extra blank lines are how people type, not a structure to preserve. */
  it('ignores runs of blank lines', () => {
    expect(parseContentBody('اول\n\n\n\nدوم', 'قوانین').blocks).toEqual([{ body: ['دوم'] }]);
  });

  /** A heading with nothing under it is a heading the shop is still writing. */
  it('drops a heading with no text beneath it', () => {
    expect(parseContentBody('مقدمه\n\n## خالی', 'قوانین').blocks).toEqual([]);
  });

  /** `###` too — nobody reads a rule that says which level to use. */
  it('accepts a third-level heading as a section', () => {
    expect(parseContentBody('مقدمه\n\n### بخش\n\nمتن', 'قوانین').blocks).toEqual([
      { title: 'بخش', body: ['متن'] },
    ]);
  });

  /**
   * A shop that opens its page with a heading has no intro paragraph. The
   * heading's own section must still survive — dropping it would lose the top
   * of the document.
   */
  it('handles a body that starts with a heading', () => {
    const { intro, blocks } = parseContentBody('## بخش اول\n\nمتن', 'قوانین');

    expect(intro).toBe('');
    expect(blocks).toEqual([{ title: 'بخش اول', body: ['متن'] }]);
  });

  /** An empty body is an empty page, not a crash. */
  it('survives a body with nothing in it', () => {
    expect(parseContentBody('   \n\n  ', 'قوانین')).toEqual({ intro: '', blocks: [] });
  });
});
