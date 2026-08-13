import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { formatNumber } from '@bojan/ui';
import type { Cart, Product } from '@/lib/api/types';
import { CartProvider, useCart } from './store';

const SHIPPING = 45_000;

function makeProduct(overrides: Partial<Product> = {}): Product {
  return {
    id: 'p-01',
    slug: 'daily-planner',
    title: 'دفتر پلنر روزانه',
    brand: 'بوژان استودیو',
    brandSlug: 'bojan-studio',
    categorySlug: 'notebooks',
    categoryName: 'دفتر و پلنر',
    price: 200_000,
    rating: 4.5,
    reviewCount: 12,
    stock: 5,
    image: '/p.jpg',
    imageAlt: 'دفتر',
    isNew: false,
    isBestseller: false,
    ...overrides,
  };
}

/** Reads the cart out of context and exposes its mutations as buttons. */
function Probe() {
  const { cart, count, hydrated, addItem, setQuantity, removeItem, applyCoupon, clear } = useCart();

  return (
    <div>
      <span data-testid="hydrated">{String(hydrated)}</span>
      <span data-testid="count">{count}</span>
      <span data-testid="lines">{cart.lines.length}</span>
      {/*
        The units on each line, separately from `count`.

        Most of the assertions below are about clamping a quantity — to the
        stock, to the per-line ceiling — and they used to read `count`, which
        was the sum of every line's units and so stood in for one line's
        quantity as long as there was only one line. `count` is the badge on the
        cart icon and now counts lines rather than units, which is what a
        shopper reads it as. So the clamping tests read the quantity itself,
        which is what they were always about.
      */}
      <span data-testid="units">{cart.lines.reduce((sum, line) => sum + line.quantity, 0)}</span>
      <span data-testid="subtotal">{formatNumber(cart.subtotal)}</span>
      <span data-testid="discount">{formatNumber(cart.discount)}</span>
      <span data-testid="shipping">{formatNumber(cart.shipping)}</span>
      <span data-testid="total">{formatNumber(cart.total)}</span>

      <button onClick={() => addItem(makeProduct(), 1)}>add</button>
      <button onClick={() => addItem(makeProduct(), 4)}>add-four</button>
      <button onClick={() => addItem(makeProduct({ id: 'p-02', slug: 'pen' }), 1)}>add-other</button>
      <button onClick={() => setQuantity('line-p-01', 3)}>set-three</button>
      <button onClick={() => setQuantity('line-p-01', 999)}>set-absurd</button>
      <button onClick={() => setQuantity('line-p-01', Number.NaN)}>set-nan</button>
      <button onClick={() => addItem(makeProduct({ id: 'p-03', slug: 'ink', stock: 500 }), 1)}>
        add-plentiful
      </button>
      <button onClick={() => setQuantity('line-p-03', 999)}>set-absurd-plentiful</button>
      <button onClick={() => removeItem('line-p-01')}>remove</button>
      <button onClick={() => applyCoupon('BOJAN10', 120_000)}>coupon</button>
      <button onClick={() => applyCoupon('HUGE', 10_000_000)}>coupon-huge</button>
      <button onClick={() => clear()}>clear</button>
    </div>
  );
}

function setup(seed?: Cart) {
  render(
    <CartProvider shipping={SHIPPING} {...(seed ? { seed } : null)}>
      <Probe />
    </CartProvider>,
  );
  return userEvent.setup();
}

const read = (id: string) => screen.getByTestId(id).textContent;

describe('CartProvider', () => {
  const realFetch = globalThis.fetch;

  beforeEach(() => {
    window.localStorage.clear();
  });

  afterEach(() => {
    globalThis.fetch = realFetch;
  });

  it('starts empty and hydrates after mount', async () => {
    setup();

    expect(read('hydrated')).toBe('true');
    expect(read('lines')).toBe('0');
    expect(read('count')).toBe('0');
  });

  it('adds a product and derives the totals', async () => {
    const user = setup();
    await user.click(screen.getByText('add'));

    expect(read('lines')).toBe('1');
    expect(read('subtotal')).toBe(formatNumber(200_000));
    expect(read('shipping')).toBe(formatNumber(SHIPPING));
    expect(read('total')).toBe(formatNumber(245_000));
  });

  it('bumps the existing line instead of adding a second one', async () => {
    const user = setup();
    await user.click(screen.getByText('add'));
    await user.click(screen.getByText('add'));

    expect(read('lines')).toBe('1');
    expect(read('units')).toBe('2');
    // One product, added twice — the badge still says one thing in the basket.
    expect(read('count')).toBe('1');
    expect(read('subtotal')).toBe(formatNumber(400_000));
  });

  it('never exceeds the product stock', async () => {
    const user = setup();
    // Stock is 5; four plus four would be eight.
    await user.click(screen.getByText('add-four'));
    await user.click(screen.getByText('add-four'));

    expect(read('units')).toBe('5');
  });

  /**
   * This asserted 20 — the per-line ceiling — because `setQuantity` was the one
   * mutation that did not consider stock. Adding to the basket clamped to it,
   * changing the quantity afterwards did not, so a shopper could take a
   * five-in-stock product to twenty from the cart page and be refused four
   * screens later at the moment of payment, with a message telling them to try
   * again in a bit.
   */
  it('clamps a quantity change to the stock, not merely to the per-line ceiling', async () => {
    const user = setup();
    await user.click(screen.getByText('add'));
    await user.click(screen.getByText('set-absurd'));

    expect(read('units')).toBe('5');
  });

  it('still clamps to the per-line ceiling where stock is not the tighter limit', async () => {
    const user = setup();
    await user.click(screen.getByText('add-plentiful'));
    await user.click(screen.getByText('set-absurd-plentiful'));

    expect(read('units')).toBe('20');
  });

  it('refuses a non-finite quantity rather than rendering NaN everywhere', async () => {
    const user = setup();
    await user.click(screen.getByText('add'));
    await user.click(screen.getByText('set-nan'));

    // NaN survived the clamp and poisoned the line, the subtotal, the total and
    // the header count — then the line vanished on the next visit, because
    // storage round-trips NaN as null and the guard rejects it.
    expect(read('count')).toBe('1');
    expect(read('subtotal')).toBe(formatNumber(200_000));
  });

  it('drops a stored line whose price is not a positive number', async () => {
    window.localStorage.setItem(
      'bojan.cart.v1',
      JSON.stringify({
        v: 1,
        discount: 0,
        lines: [
          {
            id: 'line-tampered',
            productId: 'p-01',
            slug: 'x',
            title: 'x',
            image: '/x.jpg',
            unitPrice: -1_000_000,
            quantity: 1,
          },
        ],
      }),
    );

    setup();

    // A negative price rendered a negative subtotal and a negative discount
    // line beside it.
    expect(read('lines')).toBe('0');
  });

  it('charges no shipping on an empty basket', async () => {
    const user = setup();
    await user.click(screen.getByText('add'));
    await user.click(screen.getByText('remove'));

    expect(read('shipping')).toBe('۰');
    expect(read('total')).toBe('۰');
  });

  it('caps a discount at the value of the goods', async () => {
    const user = setup();
    await user.click(screen.getByText('add'));
    await user.click(screen.getByText('coupon-huge'));

    // Subtotal is 200,000 — the discount cannot exceed it and the total cannot
    // go negative.
    expect(read('discount')).toBe(formatNumber(200_000));
    expect(read('total')).toBe(formatNumber(SHIPPING));
  });

  it('drops the coupon when the last line is removed', async () => {
    const user = setup();
    await user.click(screen.getByText('add'));
    await user.click(screen.getByText('coupon'));
    expect(read('discount')).toBe(formatNumber(120_000));

    await user.click(screen.getByText('remove'));
    expect(read('discount')).toBe('۰');
  });

  it('persists across a remount', async () => {
    const user = setup();
    await user.click(screen.getByText('add'));
    await user.click(screen.getByText('add-other'));

    screen.getByTestId('lines').remove();
    act(() => {
      // A fresh provider reads what the first one wrote.
    });

    render(
      <CartProvider shipping={SHIPPING}>
        <Probe />
      </CartProvider>,
    );

    expect(screen.getAllByTestId('lines').at(-1)?.textContent).toBe('2');
  });

  it('seeds a first-time visitor but not a returning one', async () => {
    const seed: Cart = {
      id: 'demo',
      lines: [
        {
          id: 'line-demo',
          productId: 'p-99',
          slug: 'demo',
          title: 'نمونه',
          brand: 'بوژان',
          image: '/d.jpg',
          unitPrice: 50_000,
          quantity: 2,
        },
      ],
      subtotal: 100_000,
      discount: 0,
      shipping: SHIPPING,
      total: 145_000,
    };

    const user = setup(seed);
    expect(read('lines')).toBe('1');
    expect(read('units')).toBe('2');
    // One product, added twice — the badge still says one thing in the basket.
    expect(read('count')).toBe('1');

    // Once the shopper has edited the basket, the seed must not come back.
    await user.click(screen.getByText('clear'));
    expect(read('lines')).toBe('0');

    render(
      <CartProvider shipping={SHIPPING} seed={seed}>
        <Probe />
      </CartProvider>,
    );
    expect(screen.getAllByTestId('lines').at(-1)?.textContent).toBe('0');
  });

  it('ignores a corrupt stored cart rather than crashing', () => {
    window.localStorage.setItem('bojan.cart.v1', '{ not json');
    setup();

    expect(read('hydrated')).toBe('true');
    expect(read('lines')).toBe('0');
  });

  it('discards stored lines that are not shaped like cart lines', () => {
    window.localStorage.setItem(
      'bojan.cart.v1',
      JSON.stringify({
        v: 1,
        discount: 0,
        lines: [{ id: 'x' }, { id: 'y', productId: 2, slug: null }],
      }),
    );
    setup();

    expect(read('lines')).toBe('0');
  });

  /**
   * The discount was stored and left alone through every line change, so a
   * coupon worth 120,000 on a 400,000 basket stayed worth 120,000 after the
   * basket dropped to 200,000 — the summary showed a number the server would
   * never charge.
   */
  it('stops claiming a discount that was priced for a different basket', async () => {
    const answered = new Promise<void>((resolve) => {
      globalThis.fetch = vi.fn(async (input: RequestInfo | URL) => {
        if (String(input).includes('/api/cart/coupon')) resolve();
        return new Response('{}', { status: 500 });
      }) as typeof fetch;
    });

    const user = setup();
    await user.click(screen.getByText('add-four'));
    await user.click(screen.getByText('coupon'));
    expect(read('discount')).toBe(formatNumber(120_000));

    // One unit removed, and the amount is no longer this basket's.
    await user.click(screen.getByText('set-three'));
    expect(read('discount')).toBe(formatNumber(0));

    // And the code is re-priced rather than the shopper being made to type it
    // again — the request going out is what says so.
    await act(async () => {
      await answered;
    });
  });

  it('refreshes stored prices against the catalogue on hydration', async () => {
    window.localStorage.setItem(
      'bojan.cart.v1',
      JSON.stringify({
        v: 1,
        discount: 0,
        lines: [
          {
            id: 'line-p-01',
            productId: 'p-01',
            slug: 'daily-planner',
            title: 'دفتر پلنر روزانه',
            image: '/p.jpg',
            // What it cost the day it went in the basket.
            unitPrice: 200_000,
            quantity: 2,
            stock: 5,
          },
        ],
      }),
    );

    globalThis.fetch = vi.fn(async () =>
      Response.json({ lines: [{ slug: 'daily-planner', price: 260_000, stock: 5 }] }),
    ) as typeof fetch;

    await act(async () => {
      setup();
    });

    expect(read('subtotal')).toBe(formatNumber(520_000));
  });

  it('drops a stored line the catalogue no longer sells', async () => {
    window.localStorage.setItem(
      'bojan.cart.v1',
      JSON.stringify({
        v: 1,
        discount: 0,
        lines: [
          {
            id: 'line-p-01',
            productId: 'p-01',
            slug: 'discontinued',
            title: 'محصول حذف‌شده',
            image: '/p.jpg',
            unitPrice: 200_000,
            quantity: 1,
          },
        ],
      }),
    );

    globalThis.fetch = vi.fn(async () => Response.json({ lines: [] })) as typeof fetch;

    await act(async () => {
      setup();
    });

    // An empty answer means nothing was resolved, which is indistinguishable
    // from a failed lookup — so the basket is left alone rather than emptied.
    expect(read('lines')).toBe('1');
  });
});
