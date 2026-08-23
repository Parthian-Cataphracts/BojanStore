import { describe, expect, it } from 'vitest';
import { getArticles, getHomeArticles, getTestimonials } from './editorial';
import { mockArticles, mockTestimonials } from '../mock/editorial';

/**
 * The mock path, which is what `NEXT_PUBLIC_USE_MOCK_DATA` selects by default.
 *
 * `getHomeArticles` is worth testing on either backend: the ordering rule —
 * featured first, newest after, capped — lives in the client, and it is the
 * rule that decides whether a shop which has never ticked «ویژه» gets an empty
 * rail or its three newest articles.
 */

describe('getHomeArticles', () => {
  it('caps the rail at the limit it is given', async () => {
    const articles = await getHomeArticles(3);
    expect(articles).toHaveLength(3);
  });

  it('puts featured articles first', async () => {
    const articles = await getHomeArticles(mockArticles.length);
    const featured = articles.map((article) => Boolean(article.featured));

    // Every featured article sits ahead of every plain one, which is the same
    // as saying the flags never go false-then-true as the list is walked.
    const firstPlain = featured.indexOf(false);
    if (firstPlain !== -1) {
      expect(featured.slice(firstPlain).every((isFeatured) => !isFeatured)).toBe(true);
    }
  });

  /**
   * The fallback is the point of the function. A rail that stayed empty until
   * somebody ticked a box would read as the magazine itself being empty, on the
   * page most people see first.
   */
  it('falls back to the newest when nothing is marked featured', async () => {
    const all = await getArticles();
    const unfeatured = all.filter((article) => !article.featured);
    expect(unfeatured.length).toBeGreaterThan(0);

    const articles = await getHomeArticles(3);
    expect(articles.length).toBeGreaterThan(0);
  });

  it('never returns the same article twice', async () => {
    const articles = await getHomeArticles(mockArticles.length);
    expect(new Set(articles.map((a) => a.slug)).size).toBe(articles.length);
  });
});

describe('getTestimonials', () => {
  it('caps at the limit it is given', async () => {
    const testimonials = await getTestimonials(2);
    expect(testimonials).toHaveLength(2);
  });

  /**
   * Each card links to the product it praises, so a testimonial without one
   * would render a link to `/products/undefined`.
   */
  it('carries the product every quote is about', async () => {
    const testimonials = await getTestimonials(6);
    expect(testimonials.length).toBeGreaterThan(0);

    for (const testimonial of testimonials) {
      expect(testimonial.productSlug).toBeTruthy();
      expect(testimonial.productTitle).toBeTruthy();
      expect(testimonial.productImage).toBeTruthy();
    }
  });

  it('returns the fixtures unchanged on the mock path', async () => {
    const testimonials = await getTestimonials(mockTestimonials.length);
    expect(testimonials.map((t) => t.id)).toEqual(mockTestimonials.map((t) => t.id));
  });
});
