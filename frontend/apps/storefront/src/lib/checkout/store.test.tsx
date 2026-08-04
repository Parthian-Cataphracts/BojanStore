import { act, renderHook, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { CheckoutSelectionProvider, useCheckoutSelection } from './store';

/**
 * The guided checkout's choices have to outlive the navigation between its
 * steps — that is the whole reason this store exists. Each step used to hold
 * its answer in local state, so pressing "continue" discarded it and the
 * review screen showed the first option of every list.
 */

const STORAGE_KEY = 'bojan.checkout.v1';

function wrapper({ children }: { children: React.ReactNode }) {
  return <CheckoutSelectionProvider>{children}</CheckoutSelectionProvider>;
}

async function mount() {
  const view = renderHook(() => useCheckoutSelection(), { wrapper });
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
    const { result } = await mount();
    act(() => result.current.select({ addressId: 'addr-1', shippingMethodId: 'standard' }));

    act(() => result.current.clear());

    expect(result.current.selection).toEqual({});
  });

  it('ignores a stored payload from another version', async () => {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ v: 99, addressId: 'addr-9' }));

    const { result } = await mount();
    expect(result.current.selection.addressId).toBeUndefined();
  });

  it('drops stored values that are not strings', async () => {
    window.sessionStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ v: 1, addressId: { evil: true }, shippingMethodId: 'standard' }),
    );

    const { result } = await mount();

    expect(result.current.selection.addressId).toBeUndefined();
    expect(result.current.selection.shippingMethodId).toBe('standard');
  });

  it('survives corrupt storage', async () => {
    window.sessionStorage.setItem(STORAGE_KEY, 'not json');

    const { result } = await mount();
    expect(result.current.selection).toEqual({});
  });
});
