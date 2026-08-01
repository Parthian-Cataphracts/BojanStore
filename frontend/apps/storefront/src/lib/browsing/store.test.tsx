import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useEffect } from 'react';
import { beforeEach, describe, expect, it } from 'vitest';
import type { Product } from '@/lib/api/types';
import { BrowsingProvider, useBrowsing } from './store';

const STORAGE_KEY = 'bojan.browsing.v1';

function makeProduct(id: string, title = `کالای ${id}`): Product {
  return {
    id,
    slug: `slug-${id}`,
    title,
    brand: 'بوژان استودیو',
    brandSlug: 'bojan-studio',
    categorySlug: 'notebooks',
    categoryName: 'دفتر و پلنر',
    price: 100_000,
    rating: 4,
    reviewCount: 3,
    stock: 5,
    image: '/p.jpg',
    imageAlt: '',
    isNew: false,
    isBestseller: false,
  };
}

function Probe() {
  const { viewed, terms, hydrated, recordView, recordSearch, forgetTerm, clearViewed } =
    useBrowsing();

  return (
    <div>
      <span data-testid="hydrated">{String(hydrated)}</span>
      <span data-testid="viewed">{viewed.map((p) => p.id).join(',')}</span>
      <span data-testid="terms">{terms.join(',')}</span>
      <button onClick={() => recordView(makeProduct('p-9'))}>view-9</button>
      <button onClick={() => recordView(makeProduct('p-1'))}>view-1</button>
      <button onClick={() => recordSearch('  دفتر  ')}>search-notebook</button>
      <button onClick={() => recordSearch('   ')}>search-blank</button>
      <button onClick={() => forgetTerm('دفتر')}>forget</button>
      <button onClick={() => clearViewed()}>clear-viewed</button>
    </div>
  );
}

/** Mimics the product page: a child that records during its own mount. */
function RecordsOnMount({ product }: { product: Product }) {
  const { recordView } = useBrowsing();
  useEffect(() => {
    recordView(product);
  }, [product, recordView]);
  return null;
}

const read = (id: string) => screen.getByTestId(id).textContent;

describe('BrowsingProvider', () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it('records a view, most recent first, without duplicating', async () => {
    render(
      <BrowsingProvider>
        <Probe />
      </BrowsingProvider>,
    );
    const user = userEvent.setup();

    await user.click(screen.getByText('view-9'));
    await user.click(screen.getByText('view-1'));
    expect(read('viewed')).toBe('p-1,p-9');

    // Seeing p-9 again moves it up rather than adding a second entry.
    await user.click(screen.getByText('view-9'));
    expect(read('viewed')).toBe('p-9,p-1');
  });

  it('trims and de-duplicates search terms, and ignores blank ones', async () => {
    render(
      <BrowsingProvider>
        <Probe />
      </BrowsingProvider>,
    );
    const user = userEvent.setup();

    await user.click(screen.getByText('search-notebook'));
    expect(read('terms')).toBe('دفتر');

    await user.click(screen.getByText('search-blank'));
    expect(read('terms')).toBe('دفتر');

    await user.click(screen.getByText('forget'));
    expect(read('terms')).toBe('');
  });

  it('keeps a view recorded before storage was read', () => {
    // React runs child effects before the provider's, so this view lands
    // first. Hydration used to replace state outright and lose it — which is
    // precisely the visit the shopper is making.
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ v: 1, viewed: [makeProduct('p-old')], terms: [] }),
    );

    render(
      <BrowsingProvider>
        <RecordsOnMount product={makeProduct('p-now')} />
        <Probe />
      </BrowsingProvider>,
    );

    expect(read('viewed')).toBe('p-now,p-old');
  });

  it('prefers stored history over the demo seed', () => {
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ v: 1, viewed: [makeProduct('p-mine')], terms: ['مال من'] }),
    );

    render(
      <BrowsingProvider seedViewed={[makeProduct('p-demo')]} seedTerms={['نمونه']}>
        <Probe />
      </BrowsingProvider>,
    );

    expect(read('viewed')).toBe('p-mine');
    expect(read('terms')).toBe('مال من');
  });

  it('seeds a first-time visitor', () => {
    render(
      <BrowsingProvider seedViewed={[makeProduct('p-demo')]} seedTerms={['نمونه']}>
        <Probe />
      </BrowsingProvider>,
    );

    expect(read('viewed')).toBe('p-demo');
    expect(read('terms')).toBe('نمونه');
  });

  it('discards stored entries that are not shaped like products', () => {
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ v: 1, viewed: [{ id: 'x' }, { id: 2, slug: null }], terms: [1, '', 'ok'] }),
    );

    render(
      <BrowsingProvider>
        <Probe />
      </BrowsingProvider>,
    );

    expect(read('viewed')).toBe('');
    expect(read('terms')).toBe('ok');
  });

  it('ignores a corrupt store rather than crashing', () => {
    window.localStorage.setItem(STORAGE_KEY, '{ not json');

    render(
      <BrowsingProvider>
        <Probe />
      </BrowsingProvider>,
    );

    expect(read('hydrated')).toBe('true');
    expect(read('viewed')).toBe('');
  });

  it('clears the viewed list', async () => {
    render(
      <BrowsingProvider seedViewed={[makeProduct('p-demo')]}>
        <Probe />
      </BrowsingProvider>,
    );
    const user = userEvent.setup();

    await user.click(screen.getByText('clear-viewed'));
    expect(read('viewed')).toBe('');
  });
});
