/**
 * The shop's own details — everything the storefront renders that is not a
 * product.
 *
 * The name in the header, the contact block in the footer, the delivery promise
 * on a product page, the free-shipping line in the cart. All of it used to be
 * written into the components: an owner who moved office, changed their support
 * number or raised their free-shipping threshold needed a developer and a
 * deploy. Now it is a settings screen, and this is the one read behind it.
 *
 * Every field has a fallback, and the fallback is exactly what the components
 * used to say. A shop that has never opened the settings screen looks as it did
 * before; a backend that cannot be reached renders a page rather than an error.
 */

import { toLatinDigits } from '@bojan/ui';
import { api, useMockData } from './client';

export interface StoreIdentity {
  name: string;
  tagline: string;
  description: string;
}

export interface StoreContact {
  phone: string;
  email: string;
  businessPhone: string;
  businessEmail: string;
  address: string;
  postalCode: string;
  workingHours: string;
}

export interface StoreSocial {
  instagram: string;
  telegram: string;
  whatsapp: string;
  linkedin: string;
}

export interface StorePromises {
  /** Toman above which delivery is free. Zero for a shop that never offers it. */
  returnWindowDays: number;
  deliveryEstimate: string;
  supportPromise: string;
}

export interface StoreSettings {
  identity: StoreIdentity;
  contact: StoreContact;
  social: StoreSocial;
  promises: StorePromises;
}

/**
 * What the shop looked like before any of this was configurable.
 *
 * Kept as one object rather than scattered `??` defaults so there is a single
 * answer to "what does an unconfigured shop say", and so the storefront's own
 * copy and the API's fallbacks cannot drift apart.
 */
export const storeDefaults: StoreSettings = {
  identity: {
    name: 'بوژان',
    tagline: 'برای لحظه‌های خلاق زندگی',
    description: '',
  },
  contact: {
    phone: '',
    email: '',
    businessPhone: '',
    businessEmail: '',
    address: '',
    postalCode: '',
    workingHours: '',
  },
  social: { instagram: '', telegram: '', whatsapp: '', linkedin: '' },
  promises: {
    returnWindowDays: 7,
    deliveryEstimate: '۲ تا ۵ روز کاری',
    supportPromise: '',
  },
};

/**
 * Cached for five minutes, matching the API's own cache header.
 *
 * It is read on nearly every page, and it changes when an owner edits a form —
 * so revalidating rather than fetching per request, and a window short enough
 * that a correction to a phone number is live before anyone rings the old one.
 */
const STORE_REVALIDATE = 300;

export async function getStoreSettings(): Promise<StoreSettings> {
  if (useMockData) return storeDefaults;

  const settings = await api
    .get<Partial<StoreSettings>>('/store/settings', {
      next: { revalidate: STORE_REVALIDATE, tags: ['store-settings'] },
    })
    .catch(() => null);

  if (!settings) return storeDefaults;

  // Merged per group rather than trusted whole: an older API that has not
  // learned about `promises` yet would otherwise leave the cart with no
  // threshold at all, which reads as "no free delivery ever".
  return {
    identity: { ...storeDefaults.identity, ...settings.identity },
    contact: { ...storeDefaults.contact, ...settings.contact },
    social: { ...storeDefaults.social, ...settings.social },
    promises: { ...storeDefaults.promises, ...settings.promises },
  };
}

/**
 * A social handle as a link, or null when the shop has not set one.
 *
 * The owner types whichever they have to hand — a handle, a full URL, a phone
 * number for WhatsApp — so this normalises rather than demanding a format on a
 * settings screen nobody wants to read the rules of.
 */
export function socialUrl(platform: keyof StoreSocial, value: string): string | null {
  const handle = value.trim();
  if (handle.length === 0) return null;

  if (handle.startsWith('http://') || handle.startsWith('https://')) return handle;

  const bare = handle.replace(/^@/, '');

  switch (platform) {
    case 'instagram':
      return `https://instagram.com/${bare}`;
    case 'telegram':
      return `https://t.me/${bare}`;
    case 'whatsapp':
      // Digits only — wa.me rejects a number with spaces or a leading plus.
      // Converted from Persian first: `\d` matches ASCII only, so a number the
      // owner typed in Persian digits would otherwise be stripped to nothing.
      return `https://wa.me/${toLatinDigits(bare).replace(/[^\d]/g, '')}`;
    case 'linkedin':
      return `https://linkedin.com/company/${bare}`;
    default:
      return null;
  }
}

/** One rung of the loyalty club, as the shop configured it. */
export interface LoyaltyTier {
  name: string;
  minimumPoints: number;
  discountPercent: number;
  freeShipping: boolean;
}

export interface LoyaltyProgramme {
  enabled: boolean;
  tomanPerPoint: number;
  tiers: LoyaltyTier[];
}

/**
 * The loyalty club.
 *
 * Off when the shop has configured no tiers, which is how a shop says it runs
 * no club — the page then says so rather than drawing the three tiers this
 * application used to invent. Those were hard-coded here for two years while
 * nothing in the system awarded a point or applied a discount, so the page was
 * a promise the shop could not keep.
 */
export async function getLoyaltyProgramme(): Promise<LoyaltyProgramme> {
  const off: LoyaltyProgramme = { enabled: false, tomanPerPoint: 0, tiers: [] };

  if (useMockData) return off;

  return api
    .get<LoyaltyProgramme>('/loyalty', { next: { revalidate: 300, tags: ['loyalty'] } })
    .catch(() => off);
}
