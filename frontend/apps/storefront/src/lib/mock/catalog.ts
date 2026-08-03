import type { Address, Cart, Category, OrderSummary, User } from '../api/types';
import { mockOrderDetails } from './orders';
import { mockProducts } from './products';

/** Category tiles as drawn on the homepage and the categories screen. */
export const mockCategories: Category[] = [
  {
    slug: 'stationery',
    name: 'نوشت‌افزار',
    icon: 'edit',
    productCount: mockProducts.filter((p) => p.categorySlug === 'stationery').length,
    children: [
      { slug: 'pens', name: 'خودکار و روان‌نویس', icon: 'edit', productCount: 42 },
      { slug: 'pencils', name: 'مداد و اتود', icon: 'draw', productCount: 31 },
      { slug: 'desk', name: 'لوازم رومیزی', icon: 'desk', productCount: 24 },
    ],
  },
  {
    slug: 'notebooks',
    name: 'دفتر و پلنر',
    icon: 'book',
    productCount: mockProducts.filter((p) => p.categorySlug === 'notebooks').length,
    children: [
      { slug: 'planners', name: 'پلنر و سالنامه', icon: 'calendar_month', productCount: 28 },
      { slug: 'journals', name: 'بولت ژورنال', icon: 'menu_book', productCount: 19 },
      { slug: 'sketchbooks', name: 'دفتر طراحی', icon: 'draw', productCount: 22 },
    ],
  },
  {
    slug: 'art-tools',
    name: 'ابزار هنری',
    icon: 'palette',
    productCount: mockProducts.filter((p) => p.categorySlug === 'art-tools').length,
    children: [
      { slug: 'watercolor', name: 'آبرنگ', icon: 'water_drop', productCount: 34 },
      { slug: 'brushes', name: 'قلم‌مو', icon: 'brush', productCount: 46 },
      { slug: 'canvas', name: 'بوم و پالت', icon: 'gradient', productCount: 18 },
      { slug: 'color-pencils', name: 'مداد رنگی', icon: 'colors', productCount: 26 },
    ],
  },
  {
    slug: 'gift-lifestyle',
    name: 'هدیه و سبک زندگی',
    icon: 'redeem',
    productCount: mockProducts.filter((p) => p.categorySlug === 'gift-lifestyle').length,
    children: [
      { slug: 'ceramics', name: 'سرامیک و سفال', icon: 'local_florist', productCount: 21 },
      { slug: 'home-decor', name: 'دکور خانه', icon: 'chair', productCount: 30 },
      { slug: 'gift-boxes', name: 'باکس هدیه', icon: 'redeem', productCount: 14 },
    ],
  },
  {
    slug: 'architecture',
    name: 'طراحی و معماری',
    icon: 'architecture',
    productCount: 37,
  },
];

export const mockCart: Cart = {
  id: 'cart-demo',
  lines: mockProducts.slice(0, 3).map((product, index) => ({
    id: `line-${index + 1}`,
    productId: product.id,
    slug: product.slug,
    title: product.title,
    brand: product.brand,
    image: product.image,
    unitPrice: product.price,
    ...(product.compareAtPrice ? { compareAtPrice: product.compareAtPrice } : null),
    quantity: index === 0 ? 2 : 1,
  })),
  subtotal: 0,
  discount: 120_000,
  shipping: 45_000,
  total: 0,
  couponCode: 'BOJAN10',
};

// Derive the totals so the summary always agrees with the lines above.
mockCart.subtotal = mockCart.lines.reduce((sum, line) => sum + line.unitPrice * line.quantity, 0);
mockCart.total = mockCart.subtotal - mockCart.discount + mockCart.shipping;

export const mockUser: User = {
  id: 'u-1',
  firstName: 'نیلوفر',
  lastName: 'احمدی',
  phone: '09121234567',
  email: 'niloofar@example.com',
  birthDate: '1994-05-18',
  city: 'تهران',
  walletBalance: 850_000,
  loyaltyPoints: 1_240,
};

export const mockAddresses: Address[] = [
  {
    id: 'addr-1',
    title: 'خانه',
    recipient: 'نیلوفر احمدی',
    phone: '09121234567',
    province: 'تهران',
    city: 'تهران',
    postalCode: '1968843561',
    line: 'خیابان ولیعصر، بالاتر از پارک ساعی، کوچه شهید نیکنام، پلاک ۱۲، واحد ۴',
    isDefault: true,
  },
  {
    id: 'addr-2',
    title: 'محل کار',
    recipient: 'نیلوفر احمدی',
    phone: '02188776655',
    province: 'تهران',
    city: 'تهران',
    postalCode: '1587634512',
    line: 'خیابان سهروردی شمالی، ساختمان نگین، طبقه ۵، واحد ۱۸',
    isDefault: false,
  },
];

/** Summaries are projected from the detail fixtures so the two never disagree. */
export const mockOrders: OrderSummary[] = mockOrderDetails.map(
  ({ id, number, placedAt, status, itemCount, total, thumbnails }) => ({
    id,
    number,
    placedAt,
    status,
    itemCount,
    total,
    ...(thumbnails ? { thumbnails } : null),
  }),
);

/** Products the user has saved — screen 11. */
export const mockWishlist = [
  mockProducts[8]!,
  mockProducts[0]!,
  mockProducts[11]!,
  mockProducts[10]!,
  mockProducts[13]!,
  mockProducts[18]!,
];
