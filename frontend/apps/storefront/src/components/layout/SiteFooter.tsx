import Link from 'next/link';
import { toPersianDigits } from '@bojan/ui';
import { routes } from '@/lib/routes';

const columns = [
  {
    title: 'بوژان',
    links: [
      { label: 'درباره ما', href: routes.about },
      { label: 'تماس با ما', href: routes.contact },
      { label: 'مجله بوژان', href: routes.magazine },
      { label: 'خرید سازمانی', href: routes.business },
      { label: 'باشگاه مشتریان', href: routes.loyalty },
    ],
  },
  {
    title: 'راهنما',
    links: [
      { label: 'راهنمای خرید', href: routes.buyingGuide },
      { label: 'راهنمای انتخاب محصول', href: routes.choosingGuide },
      { label: 'راهنمای سایز و مشخصات', href: routes.sizeGuide },
      { label: 'پرسش‌های متداول', href: routes.faq },
    ],
  },
  {
    title: 'خدمات مشتریان',
    links: [
      { label: 'پیگیری سفارش', href: routes.track },
      { label: 'شرایط ارسال', href: routes.shipping },
      { label: 'شرایط مرجوعی', href: routes.returnPolicy },
      { label: 'قوانین و مقررات', href: routes.terms },
      { label: 'حریم خصوصی', href: routes.privacy },
    ],
  },
];

/**
 * The desktop footer.
 *
 * Hidden below `lg`, which is exactly where `BottomNav` appears — the two are
 * complements, not neighbours. On a phone the tab bar is the navigation, and
 * fifteen more links stacked under every page is a screen and a half of scroll
 * the shopper has to swipe past to reach the end of anything. Everything the
 * footer offers is reachable from «بوژان من» or the header's search, so the
 * mobile answer is not a shorter footer but no footer.
 *
 * `hidden lg:block` rather than a JS media query: the breakpoint is a styling
 * decision, and resolving it in JavaScript would mean the server and the first
 * client render disagree about whether the element exists. The markup stays in
 * the document either way, so the links remain crawlable.
 */
export function SiteFooter() {
  const year = toPersianDigits(1405);

  return (
    <footer className="mt-xl hidden w-full border-t border-outline-variant bg-surface-container-low lg:block">
      {/*
        One column of fifteen links is a long scroll on a phone and four
        columns do not fit until `md`, so the band between them — a large phone
        turned sideways, a small tablet — gets two. Same reason the gutter has
        a tablet step: the two drawings were a phone and a desktop, and
        everything in between was inheriting whichever was nearer.
      */}
      <div className="mx-auto grid max-w-shell grid-cols-1 gap-lg px-margin-mobile py-xl sm:grid-cols-2 md:grid-cols-4 md:px-margin-tablet lg:px-margin-desktop">
        <div className="flex flex-col gap-sm">
          <span className="mb-md font-headline text-display-md font-extrabold text-primary-container">
            بوژان
          </span>
          <p className="text-body-md text-on-surface-variant">
            © {year} بوژان. تمام حقوق محفوظ است.
          </p>
        </div>

        {columns.map((column) => (
          <nav key={column.title} aria-label={column.title} className="flex flex-col gap-sm">
            <h2 className="mb-xs text-label-md font-label-md text-primary-container">
              {column.title}
            </h2>
            {column.links.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="text-body-md font-medium text-on-surface-variant transition-colors duration-200 hover:text-secondary-container"
              >
                {link.label}
              </Link>
            ))}
          </nav>
        ))}
      </div>
    </footer>
  );
}
