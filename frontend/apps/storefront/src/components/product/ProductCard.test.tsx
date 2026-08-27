import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import type { Product } from '@/lib/api/types';
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
const chooseLink = () => screen.queryByRole('link', { name: /^انتخاب گزینه/ });

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

  it('counts what is already in the basket instead of a tick that times out', async () => {
    const user = setup();

    await user.click(quickAdd()!);
    await user.click(quickAdd()!);

    expect(screen.getByText('۲')).toBeInTheDocument();
  });

  /*
    A card has no variant picker and no room to grow one.

    Adding from it put the plain product in the basket: a shopper browsing a
    grid of brushes added «a brush», with no size on the line and the stock
    taken off the parent rather than off the one they meant — and every size
    they went on to try collapsed into that same line, because a line is keyed
    on the product and the SKU together and none of them had a SKU.
  */
  describe('a product sold by combination', () => {
    it('offers the page that can ask the question, not a quick add', () => {
      setup(makeProduct({ hasVariants: true }));

      expect(quickAdd()).toBeNull();
      expect(chooseLink()).toHaveAttribute('href', '/products/round-brush');
    });

    it('puts nothing in the basket from the grid', async () => {
      const user = setup(makeProduct({ hasVariants: true }));

      // Asserted before it is pressed: clicking a control that is not there is
      // a no-op, and a basket that stayed empty because nothing was clicked
      // proves nothing about a basket that stayed empty because it should.
      const link = chooseLink();
      expect(link).not.toBeNull();
      await user.click(link!);

      expect(storedLines()).toHaveLength(0);
      expect(quickAdd()).toBeNull();
    });

    it('still quick-adds when the flag is absent, as an older payload has it', async () => {
      // `hasVariants` is optional: fixtures and anything cached from before it
      // existed omit it, and absent has to read as "no" rather than break a card.
      const user = setup(makeProduct());

      await user.click(quickAdd()!);

      expect(storedLines()).toHaveLength(1);
    });
  });
});
