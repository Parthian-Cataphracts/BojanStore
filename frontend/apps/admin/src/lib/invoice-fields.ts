/**
 * The invoice settings section and its keys.
 *
 * A module of its own, with no imports, because both sides need them: the
 * server module that reads them through the API client, and the client
 * component that posts them. Importing them from `lib/api/invoice-settings`
 * pulled `lib/api/client` — and through it `lib/auth/server` and
 * `next/headers` — into a `'use client'` bundle, which does not build.
 */

/** Matches `InvoiceSettingsDto.Section` on the API. */
export const INVOICE_SECTION = 'invoice';

/** The stored keys, in the shape `POST /admin/settings` writes them. */
export const INVOICE_KEYS = {
  sellerName: 'sellerName',
  sellerWebsite: 'sellerWebsite',
  sellerEmail: 'sellerEmail',
  sellerPhone: 'sellerPhone',
  sellerAddress: 'sellerAddress',
  sellerNationalId: 'sellerNationalId',
  sellerEconomicCode: 'sellerEconomicCode',
  thanksNote: 'thanksNote',
  terms: 'terms',
  footerNote: 'footerNote',
  stampUrl: 'stampUrl',
} as const;
