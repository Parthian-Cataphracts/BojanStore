import { describe, expect, it } from 'vitest';
import { mockCart, mockOrders, mockWishlist } from './catalog';
import { mockOrderDetails } from './orders';
import { mockQuotes } from './business';
import { mockProducts } from './products';

/*
 * Several fixtures derive their totals from their own line items instead of
 * hard-coding them. That is the point — a line change cannot leave a summary
 * stale — but it only holds if the derivation is right, so it is asserted here.
 */

describe('cart totals', () => {
  it('derives the subtotal from the line items', () => {
    const expected = mockCart.lines.reduce(
      (sum, line) => sum + line.unitPrice * line.quantity,
      0,
    );
    expect(mockCart.subtotal).toBe(expected);
  });

  it('derives the payable total as subtotal − discount + shipping', () => {
    expect(mockCart.total).toBe(mockCart.subtotal - mockCart.discount + mockCart.shipping);
  });

  it('never discounts more than the goods are worth', () => {
    expect(mockCart.discount).toBeLessThanOrEqual(mockCart.subtotal);
  });

  it('has a positive quantity on every line', () => {
    expect(mockCart.lines.every((line) => line.quantity > 0)).toBe(true);
  });
});

describe('order fixtures', () => {
  it('derives each order total from its own items', () => {
    for (const order of mockOrderDetails) {
      const subtotal = order.items.reduce(
        (sum, item) => sum + item.unitPrice * item.quantity,
        0,
      );
      expect(order.subtotal).toBe(subtotal);
      expect(order.total).toBe(order.subtotal - order.discount + order.shipping);
    }
  });

  it('reports an item count that matches the items array', () => {
    for (const order of mockOrderDetails) {
      expect(order.itemCount).toBe(order.items.length);
    }
  });

  it('projects summaries that agree with the detail records', () => {
    expect(mockOrders).toHaveLength(mockOrderDetails.length);

    for (const summary of mockOrders) {
      const detail = mockOrderDetails.find((order) => order.id === summary.id);
      expect(detail).toBeDefined();
      expect(summary.number).toBe(detail!.number);
      expect(summary.total).toBe(detail!.total);
      expect(summary.status).toBe(detail!.status);
      expect(summary.itemCount).toBe(detail!.itemCount);
    }
  });

  it('has exactly one current step, and no done step after it', () => {
    for (const order of mockOrderDetails) {
      const states = order.timeline.map((step) => step.state);
      const currentCount = states.filter((state) => state === 'current').length;

      // A fully delivered order has no current step; anything else has one.
      expect(currentCount).toBeLessThanOrEqual(1);

      const lastDone = states.lastIndexOf('done');
      const firstUpcoming = states.indexOf('upcoming');
      if (lastDone !== -1 && firstUpcoming !== -1) {
        expect(lastDone).toBeLessThan(firstUpcoming);
      }
    }
  });

  it('links every order item to a product that exists', () => {
    const slugs = new Set(mockProducts.map((product) => product.slug));
    for (const order of mockOrderDetails) {
      for (const item of order.items) {
        expect(slugs.has(item.slug)).toBe(true);
      }
    }
  });
});

describe('quote fixtures', () => {
  it('derives the subtotal, 9% VAT and total from the lines', () => {
    for (const quote of mockQuotes) {
      const subtotal = quote.lines.reduce(
        (sum, line) => sum + line.unitPrice * line.quantity,
        0,
      );
      expect(quote.subtotal).toBe(subtotal);
      expect(quote.tax).toBe(Math.round((quote.subtotal - quote.discount) * 0.09));
      expect(quote.total).toBe(quote.subtotal - quote.discount + quote.tax);
    }
  });

  it('never discounts a quote below zero', () => {
    for (const quote of mockQuotes) {
      expect(quote.discount).toBeLessThanOrEqual(quote.subtotal);
      expect(quote.total).toBeGreaterThan(0);
    }
  });
});

describe('product fixtures', () => {
  it('has unique slugs, since routes are keyed by them', () => {
    const slugs = mockProducts.map((product) => product.slug);
    expect(new Set(slugs).size).toBe(slugs.length);
  });

  it('only sets compareAtPrice when it is genuinely higher than the price', () => {
    // A compareAt equal to (or below) the price would render a 0% discount badge.
    for (const product of mockProducts) {
      if (product.compareAtPrice !== undefined) {
        expect(product.compareAtPrice).toBeGreaterThan(product.price);
      }
    }
  });

  it('keeps ratings inside the 0–5 range the star component assumes', () => {
    for (const product of mockProducts) {
      expect(product.rating).toBeGreaterThanOrEqual(0);
      expect(product.rating).toBeLessThanOrEqual(5);
    }
  });

  it('has no negative stock', () => {
    expect(mockProducts.every((product) => product.stock >= 0)).toBe(true);
  });
});

describe('wishlist fixture', () => {
  it('contains real products, not duplicates', () => {
    const ids = mockWishlist.map((product) => product.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(mockWishlist.every((product) => mockProducts.includes(product))).toBe(true);
  });
});
