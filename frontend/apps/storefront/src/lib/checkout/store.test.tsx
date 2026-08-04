import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { CheckoutProvider, useCheckout } from './store';

/**
 * The guided checkout's choices have to outlive the navigation between its
 * steps — that is the whole reason this store exists. Each step used to hold
 * its answer in local state, so pressing "continue" discarded it and the
 * review screen showed the first option of every list.
 */

const STORAGE_KEY = 'bojan.checkout.v1';

function wrapper({ children }: { children: React.ReactNode }) {
  return <CheckoutProvider>{children}</CheckoutProvider>;
}

async function mount() {
  const view = renderHook(() => useCheckout(), { wrapper });
  await waitFor(() => expect(view.result.current.hydrated).toBe(true));
  return view;
}

describe('checkout selection store', () => {
  beforeEach(() => {
    window.sessionStorage.clear();
  });

  it('starts empty', async () => {
    const { result } = await mount();
    expect(result.current.selection).toEqual({});
  });

  it('keeps each step’s answer', async () => {
    const { result } = await mount();

    act(() => result.current.select({ addressId: 'addr-2' }));
    act(() => result.current.select({ shippingMethodId: 'express' }));

    // The second step must not discard the first.
    expect(result.current.selection.addressId).toBe('addr-2');
    expect(result.current.selection.shippingMethodId).toBe('express');
  });

  it('persists across a remount, which is what leaving a step does', async () => {
    const first = await mount();
    act(() => first.result.current.select({ paymentMethodId: 'cod' }));

    await waitFor(() => expect(window.sessionStorage.getItem(STORAGE_KEY)).toContain('cod'));

    first.unmount();

    const second = await mount();
    expect(second.result.current.selection.paymentMethodId).toBe('cod');
  });

  it('clears everything once an order is placed', async () => {
    const first = await mount();
    act(() => first.result.current.select({ addressId: 'addr-1', shippingMethodId: 'standard' }));

    act(() => first.result.current.reset());
    expect(first.result.current.selection).toEqual({});

    // And stays cleared: a back-button return to the flow must not find the
    // choices that were just spent on an order.
    first.unmount();
    const second = await mount();
    expect(second.result.current.selection).toEqual({});
  });

  it('drops stored values that are not strings', async () => {
    window.sessionStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ addressId: { evil: true }, shippingMethodId: 'standard' }),
    );

    const { result } = await mount();

    // The stored object reaches the order body if it is not filtered here.
    expect(result.current.selection.addressId).toBeUndefined();
    expect(result.current.selection.shippingMethodId).toBe('standard');
  });

  it('ignores a stored array', async () => {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(['addr-1']));

    const { result } = await mount();
    expect(result.current.selection).toEqual({});
  });

  it('survives corrupt storage', async () => {
    window.sessionStorage.setItem(STORAGE_KEY, 'not json');

    const { result } = await mount();
    expect(result.current.selection).toEqual({});
  });
});
