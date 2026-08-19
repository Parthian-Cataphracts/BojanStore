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
  /**
   * Deciding a card-to-card top-up.
   *
   * Owner only, and the field list is the decision itself — no amount, no
   * customer. Both are read from the stored request on the server, so a crafted
   * body cannot credit a wallet with a number of its own choosing.
   */
  'wallet-topup-decision': {
    path: '/wallet/topups/decide',
    fields: ['id', 'approve', 'note'],
    roles: OWNER,
  },
  products: {
    path: '/products',
    fields: [
      'id',
      'title',
      'sku',
      'brand',
      // `category` is still forwarded for anything posting the older single
      // field; the form itself sends `categories`, and the API prefers it.
      'category',
      'categories',
      'collections',
      'price',
      'costPrice',
      'compareAt',
      'stock',
      'lowStock',
      'trackStock',
      'backorder',
      'status',
      'description',
      'images',
      'metaTitle',
      'metaDescription',
      'slug',
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
  /**
   * The B2B quantity breaks on one product.
   *
   * Under the catalogue role rather than a sales one: what a hundred units cost
   * to pick and ship is the same question as what one costs, so the person who
   * owns the price owns the ladder under it.
   */
  'product-volume-tiers': {
    path: '/products/volume-tiers',
    fields: ['id', 'tiers'],
    roles: CATALOGUE,
  },
  categories: {
    path: '/categories',
    fields: [
      'id',
      'title',
      'slug',
      'parentId',
      'description',
      'icon',
      'status',
      'metaTitle',
      'metaDescription',
      'showInMenu',
      'order',
    ],
    roles: CATALOGUE,
  },
  brands: {
    path: '/brands',
    fields: [
      'id',
      'title',
      'slug',
      'description',
      'logo',
      'status',
      'tagline',
      'country',
      'metaTitle',
      'metaDescription',
      'featured',
    ],
    roles: CATALOGUE,
  },
  collections: {
    path: '/collections',
    fields: [
      'id',
      'title',
      'slug',
      'description',
      'cover',
      'status',
      'summary',
      'editorialNote',
      'featured',
    ],
    roles: CATALOGUE,
  },
  /**
   * What a collection holds, and in what order.
   *
   * Its own resource rather than a field on `collections`, because the list is
   * posted whole and replaces what is stored — folding it into the entity form
   * would mean every save of a title also rewrote the membership.
   */
  'collection-products': {
    path: '/collections/products',
    fields: ['id', 'products'],
    roles: CATALOGUE,
  },
  content: {
    path: '/content',
    fields: ['id', 'title', 'slug', 'kind', 'body', 'excerpt', 'cover', 'status'],
    roles: CATALOGUE,
  },
  /**
   * A magazine article — a different table from `content` above, and the one
   * the storefront's magazine actually reads.
   *
   * The panel used to save an article as `content` with `kind: article`. That
   * writes `ContentEntry`; the magazine reads `Article`, which has its own
   * columns for the things the article page renders — editorial category,
   * reading time, the featured flag, and a body of typed blocks. Nothing joined
   * them. So an article published here appeared nowhere on the site, and every
   * article on the site was invisible here, because the seeder had written those
   * into the other table.
   *
   * No `readingMinutes`: the API derives it from the body, which is a fact
   * about the text rather than a decision to be kept in step by hand.
   */
  articles: {
    path: '/articles',
    fields: ['id', 'title', 'slug', 'excerpt', 'category', 'cover', 'body', 'featured', 'status'],
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
  /**
   * Cancelling an order.
   *
   * Its own resource rather than a value the status control can pick, because
   * it moves money and stock rather than only a label. Neither the refund nor
   * the penalty is a field: both are derived on the server from what the order
   * recorded and how far it got, so a crafted body cannot name an amount.
   * `chargePenalty` is the one judgement the operator makes — false when the
   * shop is at fault.
   */
  'order-cancel': {
    path: '/orders/cancel',
    fields: ['id', 'reason', 'chargePenalty'],
    roles: ['owner', 'sales', 'support'],
  },
  /**
   * Recording that an order's money arrived.
   *
   * Owner only, beside the wallet top-up decision and for the same reason: it
   * is a person asserting a payment against a bank statement, and asserting it
   * wrongly means goods leave the building for nothing. No amount field — what
   * the order is worth is already on the order, and a body that could name its
   * own figure would be a way to settle an order for less than it costs.
   */
  'order-payment': {
    path: '/orders/payment',
    fields: ['id', 'paid', 'reference'],
    roles: OWNER,
  },
  /**
   * Deciding a return.
   *
   * Gated with `order-cancel` rather than with `order-payment`: both give money
   * back, and both are ordinary support work. No amount field — what a return is
   * worth is derived on the server from the order's own frozen line prices, so a
   * crafted body cannot name a figure to pay a wallet. `restock` is the one
   * judgement the operator makes, and only at the warehouse step: goods that
   * have been out of the shop's hands may come back damaged.
   */
  'return-decision': {
    path: '/returns/decide',
    fields: ['id', 'status', 'note', 'restock'],
    roles: ['owner', 'sales', 'support'],
  },
  'business-requests': {
    path: '/business-requests',
    fields: ['id', 'status', 'assigneeId', 'note'],
    roles: ['owner', 'sales'],
  },
  /**
   * Issuing a pro-forma against a business request.
   *
   * `lines` carries a product id and a quantity per row; the unit prices are
   * derived on the server from the catalogue and each product's volume ladder,
   * so a crafted body cannot quote an organisation a price the shop never set.
   * `unitPriceOverride` is the one figure a line may name, and it is a rep
   * pricing a negotiated line by hand — the API still records it as the price
   * that will be charged rather than as a discount stacked on another.
   */
  'business-request-quote': {
    path: '/business-requests/quote',
    fields: ['requestId', 'lines', 'discount', 'taxRatePercent', 'validUntilUtc'],
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
  /**
   * Removing a message from the sent list.
   *
   * `kind` travels with the id because a broadcast and a one-customer message
   * are different rows in different tables, and the id alone does not say
   * which.
   */
  'notification-delete': {
    path: '/notifications/delete',
    fields: ['kind', 'id'],
    roles: ['owner', 'sales'],
  },
  /**
   * One in-app notification to one customer, from their record.
   *
   * Wider roles than `notifications` on purpose: this is a message about one
   * person's own account, sent by whoever handles that account, while the
   * broadcast above reaches the whole customer base and stays with the roles
   * trusted to do that. The API applies the same split — orders policy and the
   * customers section here, sales policy and the campaigns section there.
   *
   * The key carries no slash: the write route is `[resource]`, one segment, so
   * a key with a slash in it would arrive as two and match nothing.
   */
  'customer-notify': {
    path: '/customers/notify',
    fields: ['customerId', 'title', 'body', 'link'],
    roles: ['owner', 'sales', 'support'],
  },
  /**
   * Suspending a customer, and setting a password for one.
   *
   * Owner only, and deliberately narrower than the notify above: one closes a
   * person out of their own account and the other sets the credential to it.
   * The API refuses both for anyone else regardless of what this says — this
   * list is what stops the panel offering a control that would be refused.
   *
   * `blocked` is the state asked for rather than a toggle, so two operators on
   * the same screen agree about the result instead of undoing each other.
   */
  'customer-block': {
    path: '/customers/block',
    fields: ['customerId', 'blocked'],
    roles: ['owner'],
  },
  /**
   * The password never comes back. Nothing returns it, and the audit line
   * records only that it happened and against whom.
   */
  'customer-password': {
    path: '/customers/password',
    fields: ['customerId', 'password'],
    roles: ['owner'],
  },
  /**
   * Editing the customer record itself.
   *
   * Wider roles than the two above and deliberately so: correcting a name typed
   * wrongly at registration, or a city, is ordinary work for whoever handles
   * that customer, while closing them out of their own account and setting the
   * credential to it are not.
   *
   * No wallet balance and no loyalty points. Those are ledgers that move by a
   * transaction saying why, and a field here would be a way to credit an
   * account with no record of where the credit came from.
   */
  'customer-save': {
    path: '/customers/save',
    fields: [
      'id',
      'code',
      'firstName',
      'lastName',
      'phone',
      'email',
      'city',
      'nationalId',
      'group',
      'birthDate',
    ],
    roles: ['owner', 'sales', 'support'],
  },
  /**
   * Removing a customer outright — owner only, and irreversible.
   *
   * The API refuses it for an account that has traded: an order, a wallet
   * movement or a support ticket restricts the row, because an invoice whose
   * customer has been deleted is not a tidier invoice. Those get suspended.
   */
  'customer-delete': {
    path: '/customers/delete',
    fields: ['customerId'],
    roles: ['owner'],
  },
  /**
   * Answering a customer's email from the support mailbox.
   *
   * `to` is a field because the thread screen supplies it, but it is not free
   * text an operator typed: the screen fills it from the conversation. The
   * threading fields are what keep the reply in the customer's existing thread
   * rather than starting a new one.
   */
  'mail-reply': {
    path: '/support/mailbox/reply',
    fields: ['to', 'cc', 'subject', 'body', 'replyToFolder', 'inReplyToUid'],
    roles: ['owner', 'support'],
  },
  /**
   * The mailbox connection settings.
   *
   * Owner only, and narrower than the support role that reads the inbox: these
   * carry the credential to a mail account. Someone trusted to answer customers
   * is not thereby trusted to point the shop's support address somewhere else.
   */
  'mailbox-settings': {
    path: '/support/mailbox/settings',
    fields: [
      'enabled',
      'imapHost',
      'imapPort',
      'imapUseSsl',
      'smtpHost',
      'smtpPort',
      'smtpUseSsl',
      'username',
      'password',
      'address',
      'displayName',
    ],
    roles: OWNER,
  },
  /**
   * "Does this configuration work?" — the settings screen's test button.
   *
   * `confirm` carries nothing the API reads; its handler takes no body at all.
   * It is here because the write route refuses a request whose allowed fields
   * come to nothing, so a resource that declared none could never be posted to.
   */
  'mailbox-test': {
    path: '/support/mailbox/settings/test',
    fields: ['confirm'],
    roles: OWNER,
  },
  /**
   * Which gateway takes customers' money.
   *
   * Owner only. The merchant id decides whose account the money lands in, and
   * someone trusted to work the order queue is not thereby trusted to change
   * that. `merchantId` is write-only in both directions — the API never returns
   * it, and omitting the field is how the form says "leave the stored one".
   */
  'payment-settings': {
    path: '/payment/settings',
    fields: [
      'provider',
      'useSandboxEndpoints',
      'merchantId',
      'callbackUrl',
      'description',
      'methods',
    ],
    roles: OWNER,
  },
  /** The payment screen's test button — see `mailbox-test` for why `confirm` is here. */
  'payment-test': {
    path: '/payment/settings/test',
    fields: ['confirm'],
    roles: OWNER,
  },
  /**
   * The SMS account.
   *
   * Owner only: the key spends the shop's SMS credit and can send anything in
   * the shop's name to any number. `apiKey` is write-only, like the merchant id
   * and the mailbox password.
   */
  'sms-settings': {
    path: '/sms/settings',
    fields: ['provider', 'apiKey', 'lineNumber', 'otpTemplateId', 'otpParameterName'],
    roles: OWNER,
  },
  /**
   * Browser notifications.
   *
   * Owner only, beside SMS and the gateway. No key field in either direction:
   * the pair is generated on the server by its own route below, and a body that
   * could name a private key would be a way to make the shop sign messages with
   * somebody else's.
   */
  'push-settings': {
    path: '/push/settings',
    fields: ['enabled', 'subject'],
    roles: OWNER,
  },
  /**
   * Minting a new key pair.
   *
   * Its own resource rather than a field on the save: it disconnects every
   * browser subscribed under the old key, silently and permanently, which is not
   * something to do as a side effect of pressing save. `confirm` carries
   * nothing — see `mailbox-test` for why a field has to be here at all.
   */
  'push-keys': {
    path: '/push/settings/keys',
    fields: ['confirm'],
    roles: OWNER,
  },
  /** Sends one real message to the number the operator types. */
  'sms-test': {
    path: '/sms/settings/test',
    fields: ['phone'],
    roles: OWNER,
  },
  /**
   * The shipping tiers the checkout charges from.
   *
   * The whole list is posted and replaced, like the product variant screens.
   * Tiers are edited, never added or removed: the checkout screens name the
   * three codes as presentation constants, so a tier that exists only here is
   * unreachable and one removed here is a shopper submitting an id the shop no
   * longer has.
   */
  'shipping-methods': {
    path: '/shipping/methods',
    fields: ['methods'],
    roles: OWNER,
  },
  /**
   * The loyalty club's ladder and earning rate.
   *
   * Owner only, beside shipping and payment: a tier sets a standing discount on
   * every order every member ever places, which is the shop's margin rather
   * than one campaign's.
   */
  loyalty: {
    path: '/loyalty',
    fields: ['tomanPerPoint', 'tiers'],
    roles: OWNER,
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
  /**
   * Appointing an operator, and editing one — screen 145.
   *
   * Owner only, and the narrowest thing on this list by intent: it writes the
   * table every other entry's `roles` is checked against, so an operator who
   * could post here could grant themselves everything else.
   *
   * `password` is the initial one and the API accepts it only when there is no
   * `id`. Replacing an existing operator's password has to end the sessions
   * open on the old one, which is `admin-user-password` below rather than a
   * side effect of a save that might only be fixing a typo in a name.
   *
   * No delete, here or on the API. This row is the foreign key on the audit
   * trail, so an operator who leaves is suspended — `isActive: false` — and
   * what they did stays readable and still has their name on it.
   */
  'admin-users': {
    path: '/settings/users',
    // `identity` on create — the phone or e-mail of somebody who already has an
    // account here — and `sections` for what they may open. No `password`: an
    // operator is appointed, not minted, and their credential is the one they
    // registered with.
    fields: ['id', 'identity', 'name', 'email', 'phone', 'role', 'isActive', 'sections'],
    roles: OWNER,
  },
  /* No `admin-user-password`.

     It set another operator's password for one who was locked out, and there is
     nothing left for it to set: the credential is that person's own shop
     account, so the way back in is the storefront's password-reset mail, which
     reaches them and nobody else. */

  /**
   * Lifting a second factor whose authenticator is gone. Only an id — there is
   * nothing to decide, and turning one back on is the operator's own enrolment.
   */
  'admin-user-two-factor': {
    path: '/settings/users/two-factor',
    fields: ['id'],
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
