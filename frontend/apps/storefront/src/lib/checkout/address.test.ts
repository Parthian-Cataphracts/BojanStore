import { describe, expect, it } from 'vitest';
import { describeAddress, findChosenAddress } from './address';
import type { Address } from '@/lib/api/types';

/**
 * Screens 77 and 79 showed the shopper's *default* address while the order went
 * to the one they picked on screen 71. These assert the resolution the two
 * screens now share.
 */

const address = (id: string, over: Partial<Address> = {}): Address => ({
  id,
  title: 'خانه',
  recipient: 'مهدی',
  phone: '09120000000',
  province: 'تهران',
  city: 'تهران',
  postalCode: '1234567890',
  line: 'خیابان اول',
  isDefault: false,
  ...over,
});

const home = address('addr-home', { title: 'خانه', isDefault: true, line: 'خیابان خانه' });
const office = address('addr-office', { title: 'دفتر', line: 'خیابان دفتر' });

describe('findChosenAddress', () => {
  it('returns the address the shopper picked, not the default', () => {
    expect(findChosenAddress([home, office], 'addr-office')).toBe(office);
  });

  it('returns the default when it is the one picked', () => {
    expect(findChosenAddress([home, office], 'addr-home')).toBe(home);
  });

  it('returns nothing when no address has been chosen yet', () => {
    // Falling back to the default here is the defect: it would name an address
    // the shopper never selected on the screen that confirms the destination.
    expect(findChosenAddress([home, office], undefined)).toBeUndefined();
  });

  it('returns nothing when the stored id no longer exists', () => {
    // A deleted address must not silently resolve to a different one.
    expect(findChosenAddress([home, office], 'addr-deleted')).toBeUndefined();
  });

  it('returns nothing when the shopper has no addresses', () => {
    expect(findChosenAddress([], 'addr-home')).toBeUndefined();
  });
});

describe('describeAddress', () => {
  it('renders the one-line form the checkout screens show', () => {
    expect(describeAddress(office)).toBe('تهران، تهران، خیابان دفتر');
  });

  it('is undefined for no address, so callers render their own placeholder', () => {
    expect(describeAddress(undefined)).toBeUndefined();
  });
});
