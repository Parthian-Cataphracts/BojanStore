import Link from 'next/link';
import { Icon, toPersianDigits } from '@bojan/ui';
import { getLoyaltyProgramme, getStoreSettings, socialUrl, type StoreSocial } from '@/lib/api/store';
import { routes } from '@/lib/routes';

const columns = [
  {
    title: 'فروشگاه',
    links: [
      { label: 'درباره ما', href: routes.about },
      { label: 'تماس با ما', href: routes.contact },
      { label: 'مجله', href: routes.magazine },
      { label: 'خرید سازمانی', href: routes.business },
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

const socials: { key: keyof StoreSocial; label: string; icon: string }[] = [
  { key: 'instagram', label: 'اینستاگرام', icon: 'photo_camera' },
  { key: 'telegram', label: 'تلگرام', icon: 'send' },
  { key: 'whatsapp', label: 'واتس‌اپ', icon: 'chat' },
  { key: 'linkedin', label: 'لینکدین', icon: 'work' },
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
 *
 * The shop's name, contact details and social accounts come from the settings
 * screen. They used to be written here — including the copyright year, which
 * was the literal 1405 and would have said so in 1406.
 */
export async function SiteFooter() {
  const [{ identity, contact, social }, club] = await Promise.all([
    getStoreSettings(),
    getLoyaltyProgramme(),
  ]);

  // The loyalty link only when there is a club to link to.
  //
  // The page hides itself when the shop has configured no tiers — it used to
  // invent three — and a footer that links to it anyway puts a 404 on every
  // page of the shop. Which is exactly what it did, for as long as it took to
  // deploy and check.
  const shopColumns = columns.map((column) =>
    column.title === 'فروشگاه' && club.enabled
      ? { ...column, links: [...column.links, { label: 'باشگاه مشتریان', href: routes.loyalty }] }
      : column,
  );

  // The Jalali year, derived rather than typed. `fa-IR` resolves to the Persian
  // calendar, so this is the same number the rest of the site's dates are in.
  const year = toPersianDigits(
    new Intl.DateTimeFormat('fa-IR-u-ca-persian', { year: 'numeric' })
      .format(new Date())
      .replace(/[^\d۰-۹]/g, ''),
  );

  const links = socials
    .map((item) => ({ ...item, href: socialUrl(item.key, social[item.key]) }))
    .filter((item): item is typeof item & { href: string } => item.href !== null);

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
            {identity.name}
          </span>

          {identity.description && (
            <p className="text-body-md leading-relaxed text-on-surface-variant">
              {identity.description}
            </p>
          )}

          {/* Each row only when the shop has filled it in — an empty address
              line is worse than no address line. */}
          {contact.phone && (
            <a
              href={`tel:${contact.phone}`}
              className="flex items-center gap-2xs text-body-md text-on-surface-variant transition-colors hover:text-secondary-container"
            >
              <Icon name="call" size={18} />
              {contact.phone}
            </a>
          )}

          {contact.email && (
            <a
              href={`mailto:${contact.email}`}
              className="latin flex items-center gap-2xs text-body-md text-on-surface-variant transition-colors hover:text-secondary-container"
              dir="ltr"
            >
              <Icon name="mail" size={18} />
              {contact.email}
            </a>
          )}

          {contact.address && (
            <p className="flex items-start gap-2xs text-body-md leading-relaxed text-on-surface-variant">
              <Icon name="place" size={18} className="mt-2xs shrink-0" />
              {contact.address}
            </p>
          )}

          {links.length > 0 && (
            <div className="mt-sm flex items-center gap-sm">
              {links.map((item) => (
                <a
                  key={item.key}
                  href={item.href}
                  aria-label={item.label}
                  target="_blank"
                  // An outbound link opened in a new tab keeps a handle on this
                  // one unless it is told not to.
                  rel="noreferrer noopener"
                  className="flex size-10 items-center justify-center rounded-full border border-outline-variant text-on-surface-variant transition-colors hover:border-secondary-container hover:text-secondary-container"
                >
                  <Icon name={item.icon} size={20} />
                </a>
              ))}
            </div>
          )}

          <p className="mt-sm text-caption text-on-surface-variant">
            © {year} {identity.name}. تمام حقوق محفوظ است.
          </p>
        </div>

        {shopColumns.map((column) => (
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
