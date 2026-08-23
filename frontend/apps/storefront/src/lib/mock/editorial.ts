/**
 * Collections, brand profiles and magazine articles.
 * Content transcribed from design screens 21, 22, 26, 27, 28 and 29.
 */

import type { Article, Brand, Collection, Testimonial } from '../api/types';
import { mockProducts } from './products';

const img = (index: number) => mockProducts[index]?.image ?? mockProducts[0]!.image;

/** Screen 21 — Curated collections. */
export const mockCollections: Collection[] = [
  {
    slug: 'creative-desk',
    title: 'میز کار خلاق',
    summary:
      'مجموعه‌ای از ابزارهای مینیمال و کاربردی برای ارتقای تمرکز و زیبایی فضای کاری شما.',
    cover: img(12),
    productSlugs: ['p-09', 'p-12', 'p-13', 'p-11'],
    editorialNote:
      'یک میز کار خلوت تنها به معنای زیبایی بصری نیست؛ بلکه فضایی برای تمرکز عمیق‌تر و خلاقیت روان‌تر فراهم می‌کند. ما در این کالکشن، اشیایی را گرد آورده‌ایم که در عین سادگی، کاربردی هستند و به فضای شما هویتی آرام و منسجم می‌بخشند.',
    featured: true,
  },
  {
    slug: 'back-to-school',
    title: 'شروع سال تحصیلی',
    summary: 'دفتر، پلنر و نوشت‌افزار مقاوم برای یک سال تحصیلی منظم.',
    cover: img(26),
    productSlugs: ['p-27', 'p-28', 'p-29', 'p-14'],
  },
  {
    slug: 'for-artists',
    title: 'برای هنرمندان',
    summary: 'آبرنگ، قلم‌مو و بوم برای کسانی که هر روز کار می‌کنند.',
    cover: img(0),
    productSlugs: ['p-01', 'p-02', 'p-05', 'p-06'],
  },
  {
    slug: 'for-architects',
    title: 'برای معماران',
    summary: 'ابزار طراحی و دفتر اسکچ با کاغذ مناسب راپید و ماژیک.',
    cover: img(2),
    productSlugs: ['p-03', 'p-07', 'p-18', 'p-16'],
  },
  {
    slug: 'planning',
    title: 'دفتر و برنامه‌ریزی',
    summary: 'پلنر روزانه، هفتگی و بولت ژورنال برای هر سبک برنامه‌ریزی.',
    cover: img(8),
    productSlugs: ['p-09', 'p-27', 'p-28', 'p-30'],
  },
  {
    slug: 'minimal-gifts',
    title: 'هدیه‌های مینیمال',
    summary: 'اقلام خاص و بسته‌بندی‌شده که دادنشان لذت‌بخش است.',
    cover: img(18),
    productSlugs: ['p-19', 'p-21', 'p-22', 'p-16'],
  },
];

/** Screens 26 and 27 — Brand directory and profile. */
export const mockBrands: Brand[] = [
  {
    slug: 'bojan-studio',
    name: 'بوژان استودیو',
    tagline: 'لوازم‌التحریر دست‌ساز',
    description:
      'بوژان استودیو مجموعه‌ای از محصولات منتخب برای نوشتن، برنامه‌ریزی، طراحی و زندگی روزمره است. ما به سادگی، کیفیت و زیبایی در جزئیات باور داریم.',
    cover: img(12),
    productCount: 0,
    featured: true,
  },
  {
    slug: 'winsor-newton',
    name: 'وینزور اند نیوتون',
    tagline: 'آبرنگ و رنگ حرفه‌ای',
    description:
      'برند بریتانیایی با بیش از دو قرن سابقه در تولید رنگ و آبرنگ حرفه‌ای برای هنرمندان.',
    cover: img(0),
    productCount: 0,
    featured: true,
  },
  {
    slug: 'faber-castell',
    name: 'فابر کاستل',
    tagline: 'نوشت‌افزار و مداد رنگی',
    description: 'یکی از قدیمی‌ترین تولیدکنندگان مداد و نوشت‌افزار در جهان.',
    cover: img(3),
    productCount: 0,
    featured: true,
  },
  {
    slug: 'fabriano',
    name: 'فابریانو',
    tagline: 'کاغذ و دفتر طراحی',
    description: 'کاغذسازی ایتالیایی با تمرکز بر کاغذ هنری و دفترهای طراحی.',
    cover: img(2),
    productCount: 0,
    featured: true,
  },
  {
    slug: 'rahavard',
    name: 'ره‌آورد',
    tagline: 'قلم‌مو و ابزار نقاشی',
    description: 'تولیدکننده ایرانی قلم‌مو و ابزار جانبی نقاشی.',
    cover: img(1),
    productCount: 0,
  },
  {
    slug: 'pars-art',
    name: 'پارس آرت',
    tagline: 'ابزار هنری',
    description: 'ابزار هنری با قیمت مناسب برای هنرجویان و علاقه‌مندان.',
    cover: img(5),
    productCount: 0,
  },
];

/** Screens 28 and 29 — Magazine. */
export const articleCategories = [
  'راهنمای خرید',
  'نوشت‌افزار',
  'ابزار هنری',
  'سبک زندگی',
  'توسعه فردی',
] as const;

export const mockArticles: Article[] = [
  {
    slug: 'choosing-a-planner',
    title: 'چطور یک پلنر مناسب انتخاب کنیم؟',
    excerpt:
      'انتخاب یک پلنر مناسب می‌تواند تاثیر چشمگیری در افزایش بهره‌وری و نظم شخصی شما داشته باشد. در این راهنما به بررسی انواع پلنرها و نکاتی که باید قبل از خرید به آن‌ها توجه کنید می‌پردازیم.',
    category: 'راهنمای خرید',
    cover: img(8),
    publishedAt: '2026-07-06T00:00:00Z',
    readingMinutes: 4,
    featured: true,
    recommendedProductSlug: 'p-09',
    body: [
      {
        type: 'paragraph',
        text: 'انتخاب یک پلنر مناسب می‌تواند تاثیر شگرفی بر بهره‌وری و آرامش ذهن شما داشته باشد. در دنیای پرهیاهوی امروز، داشتن ابزاری که بتواند افکار، وظایف و اهداف ما را ساماندهی کند، نه تنها یک نیاز، بلکه یک ضرورت است. اما با وجود تنوع بی‌نظیر پلنرها در بازار، از مینیمال گرفته تا جزئی‌نگر، چگونه می‌توان بهترین گزینه را انتخاب کرد؟',
      },
      { type: 'heading', text: '۱. هدف خود را مشخص کنید' },
      {
        type: 'paragraph',
        text: 'پیش از هر چیز، از خود بپرسید که چرا به یک پلنر نیاز دارید. آیا برای مدیریت پروژه‌های کاری پیچیده است؟ یا صرفاً می‌خواهید عادات روزانه خود را پیگیری کنید؟ پلنرهای روزانه برای کسانی که برنامه‌های فشرده دارند ایده‌آل است، در حالی که پلنرهای هفتگی نمای کلی‌تری از زمان به شما می‌دهند.',
      },
      { type: 'product' },
      { type: 'heading', text: '۲. به کیفیت کاغذ اهمیت دهید' },
      {
        type: 'paragraph',
        text: 'برای دوستداران نوشتن، حس کشیده شدن قلم روی کاغذ اهمیت فراوانی دارد. اگر از خودنویس یا روان‌نویس‌های جوهری استفاده می‌کنید، به دنبال پلنرهایی با گرماژ کاغذ بالا (حداقل ۹۰ گرم) باشید تا از پس‌دادن جوهر جلوگیری شود. یک کاغذ خوب، تجربه برنامه‌ریزی را از یک وظیفه به یک مراسم لذت‌بخش تبدیل می‌کند.',
      },
      { type: 'heading', text: '۳. اندازه را با سبک زندگی‌تان تطبیق دهید' },
      {
        type: 'paragraph',
        text: 'اگر پلنر همیشه همراه شماست، سایز A5 یا کوچک‌تر انتخاب منطقی‌تری است. اگر روی میز می‌ماند و فضای نوشتن بیشتری می‌خواهید، A4 گزینه بهتری است. سنگینی و ابعاد پلنر تعیین می‌کند که واقعاً از آن استفاده کنید یا نه.',
      },
    ],
  },
  {
    slug: 'best-gel-pens',
    title: 'معرفی بهترین خودکارهای ژله‌ای برای طراحی',
    excerpt:
      'خودکارهای ژله‌ای به دلیل روانی و تنوع رنگ بالا، یکی از محبوب‌ترین ابزارها برای تصویرسازی و طراحی هستند.',
    category: 'نوشت‌افزار',
    cover: img(13),
    publishedAt: '2026-06-28T00:00:00Z',
    readingMinutes: 3,
    recommendedProductSlug: 'p-14',
  },
  {
    slug: 'professional-watercolours',
    title: 'آبرنگ‌های حرفه‌ای: تفاوت‌ها و کاربردها',
    excerpt:
      'چه آبرنگی برای سبک نقاشی شما مناسب است؟ بررسی تخصصی برندهای معتبر و تفاوت رنگ‌دانه‌ها.',
    category: 'ابزار هنری',
    cover: img(0),
    publishedAt: '2026-06-24T00:00:00Z',
    readingMinutes: 7,
    recommendedProductSlug: 'p-01',
  },
  {
    slug: 'buying-a-notebook',
    title: 'راهنمای جامع خرید دفتر یادداشت باکیفیت',
    excerpt:
      'از نوع کاغذ گرفته تا صحافی، در این مقاله تمام ویژگی‌هایی که یک دفتر یادداشت عالی باید داشته باشد را مرور می‌کنیم.',
    category: 'راهنمای خرید',
    cover: img(2),
    publishedAt: '2026-06-19T00:00:00Z',
    readingMinutes: 4,
    recommendedProductSlug: 'p-03',
  },
  {
    slug: 'morning-pages',
    title: 'معجزه نوشتن افکار صبحگاهی در افزایش تمرکز',
    excerpt:
      'سه صفحه نوشتن آزاد در ابتدای روز، تمرینی ساده که ذهن را خالی و تمرکز را عمیق‌تر می‌کند.',
    category: 'توسعه فردی',
    cover: img(27),
    publishedAt: '2026-06-11T00:00:00Z',
    readingMinutes: 5,
  },
  {
    slug: 'five-desk-essentials',
    title: '۵ ابزار نوشت‌افزار که هر میز کاری به آن نیاز دارد',
    excerpt: 'فهرست کوتاهی از چیزهایی که واقعاً هر روز استفاده می‌شوند، نه آنچه فقط زیباست.',
    category: 'راهنمای خرید',
    cover: img(12),
    publishedAt: '2026-06-02T00:00:00Z',
    readingMinutes: 3,
  },
  {
    slug: 'minimal-workspace',
    title: 'طراحی فضای کار مینیمال برای آرامش بیشتر',
    excerpt: 'کمتر اما بهتر: چطور میز کار را طوری بچینیم که ذهن هم مرتب شود.',
    category: 'سبک زندگی',
    cover: img(25),
    publishedAt: '2026-05-25T00:00:00Z',
    readingMinutes: 6,
  },
];

/**
 * The home page's testimonial rail, for the mock shop.
 *
 * Written against real mock products so the cards link somewhere — a
 * testimonial whose "درباره ..." goes to a 404 is worse than no rail while
 * somebody is looking at the design.
 */
export const mockTestimonials: Testimonial[] = [
  {
    id: 'tm-1',
    author: 'نیلوفر احمدی',
    rating: 5,
    body: 'کیفیت کاغذ واقعاً بالاست و جوهر خودنویس اصلاً پس نمی‌دهد. صحافی هم محکم است و کامل باز می‌شود.',
    createdAt: '2026-07-18T00:00:00Z',
    verified: true,
    productSlug: 'p-03',
    productTitle: 'دفتر طراحی A4 جلد سخت ۱۲۰ گرمی',
    productImage: img(3),
  },
  {
    id: 'tm-2',
    author: 'سینا رستمی',
    rating: 5,
    body: 'رنگ‌ها زنده و خوش‌پخش‌اند و با کمترین آب هم به‌خوبی باز می‌شوند. برای کار حرفه‌ای کاملاً مناسب است.',
    createdAt: '2026-07-11T00:00:00Z',
    verified: true,
    productSlug: 'p-01',
    productTitle: 'آبرنگ ۲۴ رنگ حرفه‌ای',
    productImage: img(1),
  },
  {
    id: 'tm-3',
    author: 'مریم کاظمی',
    rating: 4,
    body: 'جنس موها نرم است و پرز نمی‌دهد. بسته‌بندی هم مرتب بود و سریع‌تر از چیزی که فکر می‌کردم رسید.',
    createdAt: '2026-06-29T00:00:00Z',
    verified: true,
    productSlug: 'p-02',
    productTitle: 'ست قلم‌مو ۶ عددی دست‌ساز',
    productImage: img(2),
  },
];
