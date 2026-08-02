/**
 * The panel's writable resources.
 *
 * Every admin form names one of these keys, and the write endpoint will not
 * accept a key that is not here. Each entry also lists the fields the client
 * may set: anything else in the body is dropped rather than forwarded, so a
 * crafted request cannot smuggle `role`, `costPrice` or `status` into a form
 * that was never meant to change them.
 *
 * `roles` is the permission each resource demands. The panel's own screens
 * (145-147, 154-157) are owner-only; the catalogue and content screens are open
 * to the roles that own those areas.
 */

import type { AdminRole } from '@/lib/auth/session';

export interface ResourceDefinition {
  /** Path on the .NET admin API, relative to its base URL. */
  path: string;
  fields: readonly string[];
  roles: readonly AdminRole[];
}

const ALL: readonly AdminRole[] = ['owner', 'product', 'sales', 'support'];
const OWNER: readonly AdminRole[] = ['owner'];
const CATALOGUE: readonly AdminRole[] = ['owner', 'product'];

export const resources = {
  products: {
    path: '/products',
    fields: [
      'id', 'title', 'sku', 'brand', 'category', 'price', 'costPrice',
      'stock', 'status', 'description', 'images',
    ],
    roles: CATALOGUE,
  },
  'product-pricing': {
    path: '/products/pricing',
    fields: ['id', 'price', 'costPrice', 'compareAtPrice'],
    roles: CATALOGUE,
  },
  'product-discount': {
    path: '/products/discount',
    fields: ['id', 'percent', 'amount', 'startsAt', 'endsAt'],
    roles: CATALOGUE,
  },
  // Screens 106-108. Each posts the product's whole list, so the payload is
  // the id plus one array; the API replaces rather than merges.
  'product-variants': {
    path: '/products/variants',
    fields: ['id', 'axes'],
    roles: CATALOGUE,
  },
  'product-skus': {
    path: '/products/skus',
    fields: ['id', 'skus'],
    roles: CATALOGUE,
  },
  'product-attributes': {
    path: '/products/attributes',
    fields: ['id', 'attributes'],
    roles: CATALOGUE,
  },
  categories: {
    path: '/categories',
    fields: ['id', 'title', 'slug', 'parentId', 'description', 'icon', 'status'],
    roles: CATALOGUE,
  },
  brands: {
    path: '/brands',
    fields: ['id', 'title', 'slug', 'description', 'logo', 'status'],
    roles: CATALOGUE,
  },
  collections: {
    path: '/collections',
    fields: ['id', 'title', 'slug', 'description', 'cover', 'status'],
    roles: CATALOGUE,
  },
  content: {
    path: '/content',
    fields: ['id', 'title', 'slug', 'kind', 'body', 'excerpt', 'cover', 'status'],
    roles: CATALOGUE,
  },
  campaigns: {
    path: '/campaigns',
    fields: ['id', 'title', 'kind', 'status', 'startsAt', 'endsAt', 'description'],
    roles: ['owner', 'product'],
  },
  coupons: {
    path: '/coupons',
    fields: ['id', 'code', 'percent', 'amount', 'minimumSpend', 'expiresAt', 'status'],
    roles: ['owner', 'sales'],
  },
  'stock-movements': {
    path: '/inventory/movements',
    fields: ['productId', 'kind', 'quantity', 'reason', 'reference'],
    roles: CATALOGUE,
  },
  'order-status': {
    path: '/orders/status',
    fields: ['id', 'status', 'note', 'trackingCode'],
    roles: ['owner', 'sales', 'support'],
  },
  'business-requests': {
    path: '/business-requests',
    fields: ['id', 'status', 'assigneeId', 'note'],
    roles: ['owner', 'sales'],
  },
  'support-replies': {
    path: '/support/replies',
    fields: ['threadId', 'body'],
    roles: ['owner', 'support'],
  },
  'canned-replies': {
    path: '/support/canned-replies',
    fields: ['id', 'title', 'body', 'deleted'],
    roles: ['owner', 'support'],
  },
  notifications: {
    path: '/notifications',
    fields: ['channel', 'audience', 'title', 'body', 'scheduledAt'],
    roles: ['owner', 'sales'],
  },
  'report-exports': {
    path: '/reports/export',
    fields: ['report', 'format', 'from', 'to'],
    roles: ALL,
  },
  settings: {
    path: '/settings',
    fields: ['section', 'values'],
    roles: OWNER,
  },
  backups: {
    path: '/backups',
    fields: ['kind', 'confirm'],
    roles: OWNER,
  },
  roles: {
    path: '/roles/permissions',
    fields: ['grants'],
    roles: OWNER,
  },
  'api-keys': {
    path: '/settings/api-keys',
    fields: ['id', 'label', 'scope', 'revoked'],
    roles: OWNER,
  },
  password: {
    path: '/me/password',
    fields: ['currentPassword', 'newPassword'],
    roles: ALL,
  },
  'two-factor': {
    path: '/me/2fa',
    fields: ['code', 'secret'],
    roles: ALL,
  },
} as const satisfies Record<string, ResourceDefinition>;

export type ResourceKey = keyof typeof resources;

export function isResourceKey(value: string): value is ResourceKey {
  return Object.hasOwn(resources, value);
}

/** Keep only the fields the resource declares — everything else is discarded. */
export function pickAllowedFields(
  definition: ResourceDefinition,
  body: Record<string, unknown>,
): Record<string, unknown> {
  const picked: Record<string, unknown> = {};
  for (const field of definition.fields) {
    if (field in body) picked[field] = body[field];
  }
  return picked;
}
