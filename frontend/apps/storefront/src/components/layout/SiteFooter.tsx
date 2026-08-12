import Link from 'next/link';
import { Icon, toPersianDigits } from '@bojan/ui';
import {
  getLoyaltyProgramme,
  getStoreSettings,
  socialUrl,
  type StoreSocial,
  type StoreTrustSeal,
} from '@/lib/api/store';
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
 * One trust mark, as a pill.
 *
 * A mark with nowhere to point is not a link. The registrar's badge carries a
 * verification URL and a membership usually does not, and rendering the second
 * as an anchor to `#` gives the shopper something that looks clickable, moves
 * the page to the top when they click it, and proves nothing — which is worse
 * than a plain statement, because the whole purpose of the row is to be
 * checkable.
 */
function TrustSeal({ seal }: { seal: StoreTrustSeal }) {
  const shell =
    'flex items-center gap-xs rounded-xl border border-outline-variant px-sm py-xs';

  const body = (
    <>
      <Icon name="verified" size={18} className="shrink-0 text-primary-container" />
      <span className="leading-tight">
        <span className="block text-caption font-medium text-on-surface">{seal.title}</span>
        {seal.subtitle && (
          <span className="latin block text-caption text-on-surface-variant" dir="ltr">
            {seal.subtitle}
          </span>
        )}
      </span>
    </>
  );

  if (!seal.link) {
    return <li className={shell}>{body}</li>;
  }

  return (
    <li>
      <a
        href={seal.link}
        target="_blank"
        // An outbound link opened in a new tab keeps a handle on this one
        // unless it is told not to.
        rel="noreferrer noopener"
        className={`${shell} transition-colors hover:border-secondary-container`}
      >
        {body}
      </a>
    </li>
  );
}

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
 * Laid out as a contained card rather than a full-bleed band, with the columns
 * weighted rather than equal: the shop's own block wants the room and a list of
 * four links does not, so an even five-column split left the name cramped and
 * the link columns padded with air. Contact is its own column instead of a tail
 * on the brand block, and the copyright has moved to a rule at the foot with
 * the trust marks — the two things that are about the shop as an entity rather
 * than about where to go next.
 *
 * The responsive steps are `lg` and up on purpose. Everything below that is
 * `display: none`, so the `sm:` and `md:` grid steps this used to carry never
 * applied to anything.
 *
 * The shop's name, contact details, social accounts and trust marks come from
 * the settings screen. They used to be written here — including the copyright
 * year, which was the literal 1405 and would have said so in 1406.
 */
export async function SiteFooter() {
  const [{ identity, contact, social, trustSeals }, club] = await Promise.all([
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

  // Switched off is not deleted: a mark under renewal comes off the footer for
  // a fortnight and goes back on without anybody retyping its number.
  const seals = trustSeals.filter((seal) => seal.enabled);

  // Each row only when the shop has filled it in — an empty address line is
  // worse than no address line. Phone and e-mail stay links rather than plain
  // text: on the desktop footer they are how somebody actually rings.
  const contactRows: { label: string; value: string; href?: string; latin?: boolean }[] = [
    contact.phone && { label: 'پشتیبانی', value: contact.phone, href: `tel:${contact.phone}`, latin: true },
    contact.email && { label: 'ایمیل', value: contact.email, href: `mailto:${contact.email}`, latin: true },
    contact.workingHours && { label: 'ساعات پاسخ‌گویی', value: contact.workingHours },
    contact.address && { label: 'آدرس', value: contact.address },
  ].filter(Boolean) as { label: string; value: string; href?: string; latin?: boolean }[];

  return (
    <footer className="mt-xl hidden w-full pb-lg lg:block">
      <div className="mx-auto max-w-shell px-margin-desktop">
        <div className="relative overflow-hidden rounded-32 border border-outline-variant bg-surface-container-low px-xl pb-lg pt-xl">
          {/* A hairline rather than a full border along the top edge: it reads
              as the lid of the card at the width this only ever renders at. */}
          <span
            aria-hidden
            className="absolute inset-x-0 top-0 h-px bg-gradient-to-l from-transparent via-primary-container to-transparent opacity-60"
          />

          <div className="grid gap-lg lg:grid-cols-[1.6fr_0.9fr_0.9fr_0.9fr_1.15fr]">
            <div className="flex flex-col gap-sm">
              <span className="font-headline text-display-md font-extrabold text-primary-container">
                {identity.name}
              </span>

              {identity.description && (
                <p className="max-w-[34ch] text-body-md leading-relaxed text-on-surface-variant">
                  {identity.description}
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
                      rel="noreferrer noopener"
                      className="flex size-10 items-center justify-center rounded-full border border-outline-variant text-on-surface-variant transition-colors hover:border-secondary-container hover:text-secondary-container"
                    >
                      <Icon name={item.icon} size={20} />
                    </a>
                  ))}
                </div>
              )}
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

            {contactRows.length > 0 && (
              <div className="flex flex-col gap-sm">
                <h2 className="mb-xs text-label-md font-label-md text-primary-container">تماس</h2>

                {contactRows.map((row) => (
                  <div key={row.label}>
                    <span className="block text-caption text-on-surface-variant opacity-70">
                      {row.label}
                    </span>
                    {row.href ? (
                      <a
                        href={row.href}
                        dir={row.latin ? 'ltr' : undefined}
                        className={`block text-body-md text-on-surface transition-colors hover:text-secondary-container ${row.latin ? 'latin' : ''}`}
                      >
                        {row.value}
                      </a>
                    ) : (
                      <span className="block text-body-md leading-relaxed text-on-surface">
                        {row.value}
                      </span>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* `flex-wrap-reverse` so that when the two do not fit on one line the
              marks sit above the copyright rather than below it — the notice is
              the last thing on the page in either arrangement. */}
          <div className="mt-xl flex flex-wrap-reverse items-center justify-between gap-md border-t border-outline-variant pt-lg">
            <p className="text-caption text-on-surface-variant">
              © {year} {identity.name}. تمام حقوق محفوظ است.
            </p>

            {seals.length > 0 && (
              <ul className="flex flex-wrap gap-sm">
                {seals.map((seal) => (
                  <TrustSeal key={`${seal.title}:${seal.link}`} seal={seal} />
                ))}
              </ul>
            )}
          </div>
        </div>
      </div>
    </footer>
  );
}
