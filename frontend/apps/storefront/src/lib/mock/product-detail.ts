/**
 * Reviews, questions and variant axes for the product detail screens (84-86).
 */

import type {
  ProductQuestion,
  ProductReview,
  ProductVariantAxis,
  RatingBreakdown,
} from '../api/types';

export const mockRatingBreakdown: RatingBreakdown = {
  average: 4.8,
  total: 124,
  counts: { 5: 96, 4: 18, 3: 6, 2: 3, 1: 1 },
};

export const mockProductReviews: ProductReview[] = [
  {
    id: 'pr-1',
    author: 'نیلوفر احمدی',
    rating: 5,
    body: 'کیفیت کاغذ واقعاً بالاست و جوهر خودنویس اصلاً پس نمی‌دهد. صحافی هم محکم است و کامل باز می‌شود.',
    createdAt: '2026-07-18T00:00:00Z',
    verified: true,
    helpfulCount: 24,
  },
  {
    id: 'pr-2',
    author: 'سینا رستمی',
    rating: 5,
    body: 'دقیقاً همان چیزی بود که در تصاویر دیده بودم. بسته‌بندی هم مرتب و بی‌نقص بود.',
    createdAt: '2026-07-11T00:00:00Z',
    verified: true,
    helpfulCount: 11,
  },
  {
    id: 'pr-3',
    author: 'مریم کاظمی',
    rating: 4,
    body: 'طراحی مینیمال و دلنشینی دارد. تنها ایرادش این است که تعداد صفحاتش می‌توانست بیشتر باشد.',
    createdAt: '2026-06-29T00:00:00Z',
    verified: true,
    helpfulCount: 8,
  },
  {
    id: 'pr-4',
    author: 'امیر قاسمی',
    rating: 4,
    body: 'برای برنامه‌ریزی روزانه عالی است. کاش نسخه‌ای با جلد پارچه‌ای هم داشت.',
    createdAt: '2026-06-15T00:00:00Z',
    verified: false,
    helpfulCount: 3,
  },
];

export const mockProductQuestions: ProductQuestion[] = [
  {
    id: 'q-1',
    author: 'زهرا م.',
    question: 'گرماژ کاغذ این دفتر چقدر است؟ برای ماژیک مناسب است؟',
    askedAt: '2026-07-20T00:00:00Z',
    answer: {
      author: 'پشتیبانی بوژان',
      body: 'گرماژ کاغذ ۹۰ گرم است و برای خودکار، روان‌نویس و خودنویس مناسب است. برای ماژیک‌های الکلی پیشنهاد می‌کنیم زیر برگه محافظ بگذارید.',
      answeredAt: '2026-07-21T00:00:00Z',
    },
  },
  {
    id: 'q-2',
    author: 'کاوه ر.',
    question: 'صفحاتش نقطه‌ای است یا خط‌دار؟',
    askedAt: '2026-07-14T00:00:00Z',
    answer: {
      author: 'پشتیبانی بوژان',
      body: 'صفحات نقطه‌ای (dot grid) با فاصله ۵ میلی‌متر است.',
      answeredAt: '2026-07-15T00:00:00Z',
    },
  },
  {
    id: 'q-3',
    author: 'سارا ن.',
    question: 'آیا امکان حک کردن نام روی جلد وجود دارد؟',
    askedAt: '2026-07-26T00:00:00Z',
  },
];

export const mockVariantAxes: ProductVariantAxis[] = [
  {
    id: 'color',
    label: 'رنگ',
    kind: 'swatch',
    options: [
      { id: 'cream', label: 'کرمی گرم', hex: '#EFE3D0', available: true },
      { id: 'teal', label: 'سبز‌آبی', hex: '#0f4c5c', available: true },
      { id: 'coral', label: 'مرجانی', hex: '#F36F5D', available: true },
      { id: 'charcoal', label: 'ذغالی', hex: '#2F312F', available: false },
    ],
  },
  {
    id: 'size',
    label: 'سایز',
    kind: 'chip',
    options: [
      { id: 'a5', label: 'A5', available: true },
      { id: 'a4', label: 'A4', available: true },
      { id: 'a6', label: 'A6', available: false },
    ],
  },
];

/** Screen 90 — Recent search terms. */
export const mockSearchHistory = [
  'دفترچه یادداشت خط‌دار',
  'خودکار ژله‌ای',
  'آبرنگ ۲۴ رنگ',
  'ارگانایزر رومیزی',
];
