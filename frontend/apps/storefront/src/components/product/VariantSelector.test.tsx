import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { ProductSku, ProductVariantAxis } from '@/lib/api/types';
import { VariantSelector } from './VariantSelector';

/**
 * A size axis, as the panel's variant form stores it.
 *
 * Every option is flagged available, which is the point: that flag is typed by
 * hand and nothing keeps it in step with the warehouse, so these tests are
 * about what the combinations say rather than what the form claims.
 */
function sizeAxis(numbers: number[]): ProductVariantAxis {
  return {
    id: 'size',
    label: 'سایز',
    kind: 'chip',
    options: numbers.map((n) => ({ id: `s${n}`, label: `شماره ${n}`, available: true })),
  };
}

function sku(combination: string, stock: number): ProductSku {
  return { id: `sku-${combination}`, combination, price: 100_000, stock, available: stock > 0 };
}

const chip = (n: number) => screen.getByRole('radio', { name: new RegExp(`شماره ${n}\\b`) });

describe('VariantSelector', () => {
  it('offers a size the shop actually has', () => {
    render(<VariantSelector axes={[sizeAxis([1, 2])]} skus={[sku('s1', 4), sku('s2', 2)]} />);

    expect(chip(1)).toBeEnabled();
    expect(chip(2)).toBeEnabled();
  });

  it('refuses a size whose last one has sold', () => {
    // The option's own flag still says available — the SKU is the one that knows.
    render(<VariantSelector axes={[sizeAxis([1, 2, 3])]} skus={[sku('s1', 4), sku('s2', 2), sku('s3', 0)]} />);

    expect(chip(3)).toBeDisabled();
  });

  it('refuses a size that was listed but never given a combination', () => {
    // Sizes 19 and 20 exist on the form and nowhere else. Left offered, picking
    // one sold the plain product instead — and both collapsed into one line.
    render(<VariantSelector axes={[sizeAxis([1, 19, 20])]} skus={[sku('s1', 4)]} />);

    expect(chip(1)).toBeEnabled();
    expect(chip(19)).toBeDisabled();
    expect(chip(20)).toBeDisabled();
  });

  it('opens on a size that can be bought, not merely one that exists', async () => {
    const onChange = vi.fn();
    // The first two are unbuyable, so neither may be what the page opens on:
    // landing there gives the shopper a dead button and a page that looks broken.
    render(
      <VariantSelector
        axes={[sizeAxis([1, 2, 3])]}
        skus={[sku('s1', 0), sku('s3', 5)]}
        onChange={onChange}
      />,
    );

    expect(chip(3)).toHaveAttribute('aria-checked', 'true');
    expect(onChange).toHaveBeenCalledWith('s3');
  });

  it('falls back to the form when there are no combinations to read', () => {
    // A product with axes and no SKUs at all is a different state — nothing can
    // be derived, so the flag is all there is. `AddToCartBar` still refuses to
    // sell an unresolved pick; see its own tests.
    render(
      <VariantSelector
        axes={[
          {
            id: 'size',
            label: 'سایز',
            kind: 'chip',
            options: [
              { id: 's1', label: 'شماره 1', available: true },
              { id: 's2', label: 'شماره 2', available: false },
            ],
          },
        ]}
        skus={[]}
      />,
    );

    expect(chip(1)).toBeEnabled();
    expect(chip(2)).toBeDisabled();
  });

  it('narrows one axis by what the other is set to', async () => {
    const user = userEvent.setup();
    const axes: ProductVariantAxis[] = [
      {
        id: 'colour',
        label: 'رنگ',
        kind: 'chip',
        options: [
          { id: 'cream', label: 'کرم', available: true },
          { id: 'black', label: 'مشکی', available: true },
        ],
      },
      sizeAxis([1, 2]),
    ];

    // Cream comes in both sizes; black only in size 1.
    render(
      <VariantSelector
        axes={axes}
        skus={[sku('cream|s1', 3), sku('cream|s2', 3), sku('black|s1', 3)]}
      />,
    );

    expect(chip(2)).toBeEnabled();

    await user.click(screen.getByRole('radio', { name: 'مشکی' }));

    // Size 2 exists — in cream. It is not a thing this shopper can buy now.
    expect(chip(2)).toBeDisabled();
  });
});
