import { api, useMockData } from './client';
import { INVOICE_KEYS, INVOICE_SECTION } from '@/lib/invoice-fields';
import type { InvoiceSettingsDto } from './types';

// Re-exported so server callers have one import for the whole thing. The
// definitions live in a module with no imports of its own, because the client
// form needs them too and must not pull the API client in with them.
export { INVOICE_KEYS, INVOICE_SECTION };

/**
 * What the invoice says when nothing has been configured.
 *
 * A copy of the API's `InvoiceSettingsDto.Defaults`. Duplicated rather than
 * fetched because this is what the *form* shows an owner who has never saved —
 * the document itself never reads this file, it reads the settings the API
 * resolved onto the invoice. The two agreeing matters only so the form does not
 * promise something different from what will print.
 */
export const INVOICE_DEFAULTS: InvoiceSettingsDto = {
  seller: {
    name: 'فروشگاه بوژان',
    website: 'bojanstore.com',
    email: 'support@bojanstore.com',
    phone: '',
    address: '',
    nationalId: '',
    economicCode: '',
  },
  thanksNote: 'از اعتماد و خرید شما سپاسگزاریم.',
  terms:
    'خریدار با ثبت این سفارش، قوانین و مقررات فروشگاه بوژان را مطالعه کرده و پذیرفته است. مهلت مرجوعی کالا و شرایط گارانتی، مطابق شرایط اعلام‌شده در زمان خرید است.',
  footerNote: 'این فاکتور به‌صورت الکترونیکی صادر شده و بدون مهر و امضای فیزیکی نیز معتبر است.',
  stampUrl: null,
};


/**
 * The saved invoice settings, with each unset field falling back to its
 * default.
 *
 * Per field rather than all-or-nothing, exactly as the API resolves them: an
 * owner who set only a support address must not find the rest of the form
 * blank.
 */
export async function getInvoiceSettings(): Promise<InvoiceSettingsDto> {
  if (useMockData) return INVOICE_DEFAULTS;

  const stored = await api
    .get<Record<string, string>>(`/settings/${INVOICE_SECTION}`, { auth: true })
    .catch(() => ({}) as Record<string, string>);

  const read = (key: string, fallback: string) => {
    const raw = stored[key];
    return raw === undefined || raw.trim() === '' ? fallback : unquote(raw);
  };

  const stamp = stored[INVOICE_KEYS.stampUrl];

  return {
    seller: {
      name: read(INVOICE_KEYS.sellerName, INVOICE_DEFAULTS.seller.name),
      website: read(INVOICE_KEYS.sellerWebsite, INVOICE_DEFAULTS.seller.website),
      email: read(INVOICE_KEYS.sellerEmail, INVOICE_DEFAULTS.seller.email),
      phone: read(INVOICE_KEYS.sellerPhone, ''),
      address: read(INVOICE_KEYS.sellerAddress, ''),
      nationalId: read(INVOICE_KEYS.sellerNationalId, ''),
      economicCode: read(INVOICE_KEYS.sellerEconomicCode, ''),
    },
    thanksNote: read(INVOICE_KEYS.thanksNote, INVOICE_DEFAULTS.thanksNote),
    terms: read(INVOICE_KEYS.terms, INVOICE_DEFAULTS.terms),
    footerNote: read(INVOICE_KEYS.footerNote, INVOICE_DEFAULTS.footerNote),
    // No default: absent means no stamp, and the document draws an empty box
    // to stamp by hand rather than inventing artwork.
    stampUrl: stamp && stamp.trim() !== '' ? unquote(stamp) : null,
  };
}

/**
 * Settings rows are stored as written, but a row saved by an older build — or
 * by hand — may carry JSON quotes. Stripping them here means the form shows the
 * value rather than the value wrapped in quotation marks.
 */
function unquote(raw: string): string {
  if (raw.length >= 2 && raw.startsWith('"') && raw.endsWith('"')) {
    try {
      return JSON.parse(raw) as string;
    } catch {
      return raw;
    }
  }
  return raw;
}
