import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
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
      <span data-testid="subtotal">{formatNumber(cart.subtotal)}</span>
      <span data-testid="discount">{formatNumber(cart.discount)}</span>
      <span data-testid="shipping">{formatNumber(cart.shipping)}</span>
      <span data-testid="total">{formatNumber(cart.total)}</span>

      <button onClick={() => addItem(makeProduct(), 1)}>add</button>
      <button onClick={() => addItem(makeProduct(), 4)}>add-four</button>
      <button onClick={() => addItem(makeProduct({ id: 'p-02', slug: 'pen' }), 1)}>add-other</button>
      <button onClick={() => setQuantity('line-p-01', 3)}>set-three</button>
      <button onClick={() => setQuantity('line-p-01', 999)}>set-absurd</button>
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
  beforeEach(() => {
    window.localStorage.clear();
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
    expect(read('count')).toBe('2');
    expect(read('subtotal')).toBe(formatNumber(400_000));
  });

  it('never exceeds the product stock', async () => {
    const user = setup();
    // Stock is 5; four plus four would be eight.
    await user.click(screen.getByText('add-four'));
    await user.click(screen.getByText('add-four'));

    expect(read('count')).toBe('5');
  });

  it('clamps an absurd quantity to the per-line ceiling', async () => {
    const user = setup();
    await user.click(screen.getByText('add'));
    await user.click(screen.getByText('set-absurd'));

    expect(read('count')).toBe('20');
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
    expect(read('count')).toBe('2');

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
});
