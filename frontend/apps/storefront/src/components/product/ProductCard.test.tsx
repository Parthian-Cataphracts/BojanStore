import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import type { Product, ProductSku } from '@/lib/api/types';
import { CartProvider } from '@/lib/cart/store';
import { WishlistProvider } from '@/lib/wishlist/store';
import { ProductCard } from './ProductCard';

function makeProduct(overrides: Partial<Product> = {}): Product {
  return {
    id: 'p-01',
    slug: 'round-brush',
    title: 'قلم‌مو سرگرد',
    brand: 'بوژان استودیو',
    brandSlug: 'bojan-studio',
    categorySlug: 'art-tools',
    categoryName: 'ابزار هنری',
    price: 850_000,
    rating: 4.5,
    reviewCount: 12,
    stock: 5,
    image: '/p.jpg',
    imageAlt: 'قلم‌مو',
    isNew: false,
    isBestseller: false,
    ...overrides,
  };
}

function setup(product = makeProduct()) {
  render(
    <WishlistProvider>
      <CartProvider shipping={45_000}>
        <ProductCard product={product} />
      </CartProvider>
    </WishlistProvider>,
  );
  return userEvent.setup();
}

const quickAdd = () => screen.queryByRole('button', { name: /^افزودن .* به سبد خرید$/ });

/** The first combination the shop still has, as the listing sends it. */
const sku = (overrides: Partial<ProductSku> = {}): ProductSku => ({
  id: 'sku-s1',
  combination: 's1',
  price: 825_000,
  stock: 4,
  available: true,
  ...overrides,
});

const storedLines = () =>
  JSON.parse(window.localStorage.getItem('bojan.cart.v1') ?? '{"lines":[]}').lines as unknown[];

describe('ProductCard', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('adds a plain product straight from the grid', async () => {
    const user = setup();

    await user.click(quickAdd()!);

    expect(storedLines()).toHaveLength(1);
  });

  it('keeps the same control after adding, so the grid can be filled', async () => {
    // A tile in a grid says «this went in», not how many are in the basket: a
    // number here reads as a counter to work rather than a shelf to pick from,
    // and the quantity belongs on the product page and in the basket, where
    // there is room to change it.
    const user = setup();

    await user.click(quickAdd()!);
    await user.click(quickAdd()!);

    expect(storedLines()).toHaveLength(1);
    expect((storedLines()[0] as { quantity: number }).quantity).toBe(2);
    // No count on the tile — the control is the same one it was.
    expect(screen.queryByText('۲')).toBeNull();
    expect(quickAdd()).toBeInTheDocument();
  });

  /*
    A card has no variant picker and no room to grow one.

    Adding from it used to put the plain product in the basket: a shopper
    browsing a grid of brushes added «a brush», with no size on the line and the
    stock taken off the parent rather than off the one they meant — and every
    size they went on to try collapsed into that same line, because a line is
    keyed on the product and the SKU together and none of them had a SKU.

    The tap now reserves the first combination the shop still has, which is a
    real thing at its own price rather than a link to go and choose one.
  */
  describe('a product sold by combination', () => {
    it('reserves the first combination the shop still has', async () => {
      const user = setup(makeProduct({ hasVariants: true, defaultSku: sku() }));

      await user.click(quickAdd()!);

      const line = storedLines()[0] as { skuId?: string; unitPrice: number; stock?: number };
      expect(line.skuId).toBe('sku-s1');
      // Its own price, not the product's.
      expect(line.unitPrice).toBe(825_000);
      expect(line.stock).toBe(4);
    });

    it('refuses when every combination is sold out', () => {
      // No `defaultSku` on a product that has combinations means none of them
      // has stock — the parent's own number counts nothing anybody can buy.
      setup(makeProduct({ hasVariants: true, stock: 12 }));

      expect(quickAdd()).toBeDisabled();
    });

    it('still adds the plain product when there are no combinations', async () => {
      // `hasVariants` is optional: fixtures and anything cached from before it
      // existed omit it, and absent has to read as "no" rather than break a card.
      const user = setup(makeProduct());

      await user.click(quickAdd()!);

      const line = storedLines()[0] as { skuId?: string };
      expect(storedLines()).toHaveLength(1);
      expect(line.skuId).toBeUndefined();
    });
  });
});
