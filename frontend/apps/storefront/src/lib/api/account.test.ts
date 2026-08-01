import { describe, expect, it } from 'vitest';
import { getAddress, getOrder, getOrders, getReturn, getWishlist } from './account';
import { mockOrderDetails } from '../mock/orders';

describe('getOrders', () => {
  it('returns every order when no status filter is given', async () => {
    const orders = await getOrders();
    expect(orders).toHaveLength(mockOrderDetails.length);
  });

  it('filters by status', async () => {
    const delivered = await getOrders('delivered');
    expect(delivered.length).toBeGreaterThan(0);
    expect(delivered.every((order) => order.status === 'delivered')).toBe(true);
  });

  it('returns an empty list for a status with no orders, rather than everything', async () => {
    // A filter that silently falls back to "all" is worse than an empty state.
    const pending = await getOrders('pending');
    expect(pending.every((order) => order.status === 'pending')).toBe(true);
  });
});

describe('getOrder', () => {
  it('finds an order by id', async () => {
    const order = await getOrder('o-1');
    expect(order?.id).toBe('o-1');
  });

  it('also finds it by the human-facing order number', async () => {
    // The tracking screen looks orders up by the number the customer was given.
    const byNumber = await getOrder(mockOrderDetails[0]!.number);
    expect(byNumber?.id).toBe(mockOrderDetails[0]!.id);
  });

  it('returns null for an unknown id so the page can render a 404', async () => {
    expect(await getOrder('no-such-order')).toBeNull();
  });
});

describe('getAddress', () => {
  it('finds an address by id', async () => {
    expect((await getAddress('addr-1'))?.id).toBe('addr-1');
  });

  it('returns null for an unknown id', async () => {
    expect(await getAddress('nope')).toBeNull();
  });
});

describe('getReturn', () => {
  it('finds a return by id and by its code', async () => {
    const byId = await getReturn('r-1');
    expect(byId?.id).toBe('r-1');
    expect((await getReturn('RT-BZ-204'))?.id).toBe('r-1');
  });

  it('returns null for an unknown reference', async () => {
    expect(await getReturn('RT-NOPE')).toBeNull();
  });
});

describe('getWishlist', () => {
  it('returns saved products', async () => {
    const wishlist = await getWishlist();
    expect(wishlist.length).toBeGreaterThan(0);
    expect(wishlist[0]).toHaveProperty('slug');
  });
});
