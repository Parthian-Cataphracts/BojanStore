import { safeNextPath } from '@bojan/config/safe-next';

/**
 * Carries a `?next=` destination onto a screen that stands between the customer
 * and where they were going.
 *
 * The middleware records the protected page a signed-out visitor asked for, and
 * the sign-in form sends them back to it — but only a *returning* customer. A
 * first-time one was routed to the profile step instead, and the destination
 * was dropped on the way: somebody who filled a basket, pressed «ثبت سفارش» and
 * signed up landed on their account page with the checkout they had started
 * nowhere in sight. The basket had survived — it lives in this browser — so
 * nothing was lost except the thread, and finding it again meant walking back
 * through the cart by hand.
 *
 * Validated here rather than only where it is read, so a destination that would
 * be refused on arrival is never written into the address bar to begin with.
 * `safeNextPath` falls back to the empty string for anything it will not
 * accept — absent, off-origin, or dressed up to look otherwise — and an empty
 * destination is simply not carried.
 */
export function withReturnTo(path: string, next: string | null | undefined): string {
  const destination = safeNextPath(next, '');
  return destination ? `${path}?next=${encodeURIComponent(destination)}` : path;
}

/** Central route table — keeps links honest when paths move. */
export const routes = {
  home: '/',
  categories: '/categories',
  category: (slug: string) => `/categories/${slug}`,
  products: '/products',
  product: (slug: string) => `/products/${slug}`,
  productGallery: (slug: string) => `/products/${slug}/gallery`,
  productReviews: (slug: string) => `/products/${slug}/reviews`,
  productQuestions: (slug: string) => `/products/${slug}/questions`,
  productSimilar: (slug: string) => `/products/${slug}/similar`,
  productNotify: (slug: string) => `/products/${slug}/notify`,
  offers: '/offers',
  search: '/search',
  collections: '/collections',
  collection: (slug: string) => `/collections/${slug}`,
  brands: '/brands',
  brand: (slug: string) => `/brands/${slug}`,
  newArrivals: '/new',
  bestsellers: '/bestsellers',
  gifts: '/gifts',
  article: (slug: string) => `/magazine/${slug}`,
  cart: '/cart',
  checkout: '/checkout',
  // Step-by-step checkout (screens 71-80). `/checkout` remains the single-page
  // variant from screen 08; these are the guided flow.
  checkoutAddress: '/checkout/address',
  checkoutNewAddress: '/checkout/address/new',
  checkoutShipping: '/checkout/shipping',
  checkoutDeliveryTime: '/checkout/delivery-time',
  checkoutPayment: '/checkout/payment',
  checkoutCoupon: '/checkout/coupon',
  checkoutReview: '/checkout/review',
  checkoutConfirm: '/checkout/confirm',
  checkoutSummary: '/checkout/summary',
  checkoutEdit: '/checkout/edit',
  orderPlaced: '/checkout/placed',
  paymentSuccess: '/checkout/payment/success',
  paymentFailed: '/checkout/payment/failed',
  track: '/track',
  invoice: (orderId: string) => `/account/orders/${orderId}/invoice`,
  returnRequest: (orderId: string) => `/account/orders/${orderId}/return`,
  /** The customer's own return requests — distinct from `returnPolicy`. */
  myReturns: '/account/returns',
  returnStatus: (id: string) => `/account/returns/${id}`,
  offline: '/offline',
  login: '/login',
  /** Registering is its own screen; the two link to each other. */
  register: '/register',
  forgotPassword: '/forgot-password',
  resetPassword: '/reset-password',
  account: '/account',
  orders: '/account/orders',
  addresses: '/account/addresses',
  wishlist: '/account/wishlist',
  profile: '/account/profile',
  completeProfile: '/login/complete-profile',
  notifications: '/account/notifications',
  support: '/account/support',
  reviews: '/account/reviews',
  writeReview: (slug: string) => `/account/reviews/new?product=${encodeURIComponent(slug)}`,
  recentlyViewed: '/account/recently-viewed',
  wallet: '/account/wallet',
  coupons: '/account/coupons',
  compare: '/compare',
  about: '/about',
  contact: '/contact',
  faq: '/faq',
  terms: '/terms',
  privacy: '/privacy',
  shipping: '/shipping',
  /** The public return-policy page (screen 44). */
  returnPolicy: '/returns',
  buyingGuide: '/guide',
  choosingGuide: '/guide/choosing',
  sizeGuide: '/guide/sizes',
  magazine: '/magazine',

  // Business / B2B (screens 20, 61-70)
  business: '/business',
  businessQuote: '/business/quote',
  businessBulk: '/business/bulk',
  businessOrganization: '/business/organization',
  businessRequests: '/business/requests',
  businessRequest: (id: string) => `/business/requests/${id}`,
  businessQuoteDetail: (id: string) => `/business/quotes/${id}`,
  businessGiftBoxes: '/business/gift-boxes',
  businessGuide: '/business/guide',
  businessConsultant: '/business/consultant',
  businessSubmitted: '/business/submitted',

  // Campaign landings and loyalty (screens 48-50)
  backToSchool: '/campaigns/back-to-school',
  creativeGifts: '/campaigns/creative-gifts',
  loyalty: '/loyalty',
} as const;
