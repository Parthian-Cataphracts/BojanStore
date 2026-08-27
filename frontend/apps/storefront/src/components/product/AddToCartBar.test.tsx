import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import type { Product, ProductSku } from '@/lib/api/types';
import { CartProvider } from '@/lib/cart/store';
import { AddToCartBar } from './AddToCartBar';

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

function setup(product = makeProduct(), sku?: ProductSku, requiresSku = false) {
  render(
    <CartProvider shipping={45_000}>
      <AddToCartBar product={product} {...(sku ? { sku } : null)} requiresSku={requiresSku} />
    </CartProvider>,
  );
  return userEvent.setup();
}

/*
  The phone's half of the bar.

  Desktop keeps the control it always had — a quantity chosen before anything is
  added, beside a button that confirms — and the phone gets the basket line's
  own quantity instead. Both are in the markup, and Tailwind hides whichever the
  width does not want. jsdom loads no CSS, so an unqualified query matches the
  desktop pair too; the phone's half is rendered first, and these pick it.

  `trash` needs no index: only the phone's stepper is given an `onRemove`, so it
  is the only control that offers to remove anything.
*/
const addButton = () => screen.getAllByRole('button', { name: /افزودن به سبد/ })[0]!;
const plus = () => screen.getAllByRole('button', { name: 'افزایش تعداد' })[0]!;
const trash = () => screen.getByRole('button', { name: 'حذف از سبد خرید' });

/**
 * The number the phone's control is showing.
 *
 * Read out of that control rather than off the page: desktop's own stepper
 * starts at one and would answer a bare `getByText('۱')` too.
 */
const phoneQuantity = () => {
  const pill = plus().parentElement!;
  return [...pill.children].find((child) => child.tagName === 'SPAN')!.textContent;
};

describe('AddToCartBar', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('offers to add while the basket does not hold it', () => {
    setup();
    expect(addButton()).toBeInTheDocument();
    // Only desktop's own stepper, which is not the basket's.
    expect(screen.getAllByRole('button', { name: 'افزایش تعداد' })).toHaveLength(1);
    expect(screen.queryByRole('button', { name: 'حذف از سبد خرید' })).toBeNull();
  });

  it('becomes the line quantity the moment it is added', async () => {
    const user = setup();
    await user.click(addButton());

    // No tick, no waiting for a label to go back: the number is the
    // acknowledgement, and the control that shows it is the one that changes it.
    // The phone's add button is gone — the exact name is its own; desktop's is
    // «افزودن به سبد خرید».
    expect(screen.queryByRole('button', { name: 'افزودن به سبد' })).toBeNull();
    expect(trash()).toBeInTheDocument();
    expect(phoneQuantity()).toBe('۱');
  });

  it('counts up without a pause between taps', async () => {
    const user = setup();
    await user.click(addButton());
    await user.click(plus());
    await user.click(plus());

    // Three taps, three units, nothing in between. It used to take one tap and
    // then two more through a button that spent two seconds saying "added".
    expect(phoneQuantity()).toBe('۳');
  });

  it('goes back to offering when the last one is taken out', async () => {
    const user = setup();
    await user.click(addButton());
    await user.click(trash());

    expect(addButton()).toBeInTheDocument();
  });

  it('will not count past the stock this variant has', async () => {
    const user = setup(makeProduct({ stock: 2 }));
    await user.click(addButton());
    await user.click(plus());

    expect(phoneQuantity()).toBe('۲');
    expect(plus()).toBeDisabled();
  });

  it('refuses a sold-out product', () => {
    setup(makeProduct({ stock: 0 }));
    for (const button of screen.getAllByRole('button', { name: 'ناموجود' })) {
      expect(button).toBeDisabled();
    }
  });

  it('counts the chosen variant rather than the product', async () => {
    // A page offering several models holds a line per model, so the control
    // here is the chosen one's — the whole point of picking before adding.
    const large: ProductSku = {
      id: 'sku-large',
      combination: 'large',
      price: 260_000,
      stock: 4,
      available: true,
    };

    const user = setup(makeProduct(), large);
    await user.click(addButton());
    await user.click(plus());

    expect(phoneQuantity()).toBe('۲');
  });

  it('gives each model of one product its own quantity', async () => {
    /*
      Two models of the same product, side by side in one basket — the shape a
      page with a size or colour picker takes once something has been chosen
      from it. Each is its own line, so each counts on its own: adding to the
      small one must not move the large one's number, and neither may borrow
      the other's stock.
    */
    const product = makeProduct();
    const small: ProductSku = {
      id: 'sku-small',
      combination: 'small',
      price: 200_000,
      stock: 9,
      available: true,
    };
    const large: ProductSku = {
      id: 'sku-large',
      combination: 'large',
      price: 260_000,
      stock: 9,
      available: true,
    };

    render(
      <CartProvider shipping={45_000}>
        <div data-testid="small">
          <AddToCartBar product={product} sku={small} />
        </div>
        <div data-testid="large">
          <AddToCartBar product={product} sku={large} />
        </div>
      </CartProvider>,
    );

    const user = userEvent.setup();

    /*
      Scoped per model *and* to the phone's half of each bar.

      Both matter. Reaching for «the first add button on the page» twice looks
      like two presses on the small one and is not: the first press turns that
      button into a stepper, so the second lands on the small bar's desktop
      button instead — which adds to the same line and moves neither number the
      assertions read. The test passed and proved nothing.
    */
    const phone = (model: string) => {
      const scope = within(screen.getByTestId(model));
      const stepper = () => scope.getAllByRole('button', { name: 'افزایش تعداد' })[0]!;

      return {
        add: () => scope.getAllByRole('button', { name: /افزودن به سبد/ })[0]!,
        plus: stepper,
        quantity: () =>
          [...stepper().parentElement!.children].find((child) => child.tagName === 'SPAN')!
            .textContent,
      };
    };

    // One of the small, three of the large.
    await user.click(phone('small').add());
    await user.click(phone('large').add());
    await user.click(phone('large').plus());
    await user.click(phone('large').plus());

    expect(phone('small').quantity()).toBe('۱');
    expect(phone('large').quantity()).toBe('۳');
  });

  /*
    A size an operator listed but never gave a combination.

    The bar falls back to the product's own price and stock when a pick does not
    resolve to a SKU, which is right for a product that has no combinations at
    all and wrong for one that does. On a sized product it sold the plain
    product instead: the shopper believed they had bought that size, the order
    recorded none, and it came off the plain product's shelf. Every such size
    shared the one line, too, because a line is keyed on the product and the SKU
    together and they all had the same absent SKU.
  */
  describe('a product sold by combination, on a pick that resolves to none', () => {
    it('offers nothing to buy', () => {
      setup(makeProduct(), undefined, true);

      expect(screen.queryByRole('button', { name: /افزودن به سبد/ })).toBeNull();
      for (const button of screen.getAllByRole('button', { name: 'ناموجود' })) {
        expect(button).toBeDisabled();
      }
    });

    it('leaves the basket alone when the button is pressed anyway', async () => {
      const user = setup(makeProduct(), undefined, true);

      await user.click(screen.getAllByRole('button', { name: 'ناموجود' })[0]!);

      expect(screen.queryByRole('button', { name: 'حذف از سبد خرید' })).toBeNull();
      // The basket is written on hydration whether or not anything is in it, so
      // the empty one is what "nothing was added" looks like here.
      const stored = JSON.parse(window.localStorage.getItem('bojan.cart.v1') ?? '{"lines":[]}');
      expect(stored.lines).toHaveLength(0);
    });

    it('still sells a product that has no combinations at all', async () => {
      // Axes with no SKUs is a different state — a half-filled form, or mock
      // mode, where the product's own price and stock are all there is.
      const user = setup(makeProduct(), undefined, false);

      await user.click(addButton());

      expect(trash()).toBeInTheDocument();
    });
  });
});
