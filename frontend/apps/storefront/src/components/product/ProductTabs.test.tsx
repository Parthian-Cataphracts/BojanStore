// Imported here as well as in `vitest.setup.ts`: the setup file is outside
// this app's tsconfig, so without this the matchers run but do not typecheck.
import '@testing-library/jest-dom/vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { ProductTabs } from './ProductTabs';
import type { ProductQuestion, ProductReview, RatingBreakdown } from '@/lib/api/types';

/**
 * The product page's tabs.
 *
 * Two things here are worth a test rather than a look. The first is that
 * exactly one panel shows: every panel is rendered into the DOM so crawlers
 * read the description and the specifications, and the only thing separating
 * that from all five stacked on top of each other is the `hidden` attribute —
 * which this page has already lost once, to a `display` utility on the same
 * element that outranked it.
 *
 * The second is that the panels are reachable at all. The seeded catalogue has
 * no reviews and no questions, so a shop that has just been installed shows
 * every one of these as an empty state; the version of them holding content is
 * only ever seen once a real customer writes something.
 */

const reviews: ProductReview[] = [
  {
    id: 'r-1',
    author: 'نگار',
    rating: 5,
    body: 'رنگ‌ها دقیقاً همان‌طور که در تصویر بود.',
    createdAt: '2026-05-01T10:00:00Z',
    verified: true,
    helpfulCount: 4,
  },
  {
    id: 'r-2',
    author: 'کامران',
    rating: 4,
    body: 'کیفیت مو خوب است، جعبه کمی آسیب دیده بود.',
    createdAt: '2026-05-04T10:00:00Z',
    verified: false,
    helpfulCount: 1,
  },
];

const breakdown: RatingBreakdown = {
  average: 4.5,
  total: 2,
  counts: { 1: 0, 2: 0, 3: 0, 4: 1, 5: 1 },
};

const questions: ProductQuestion[] = [
  {
    id: 'q-1',
    author: 'سارا',
    question: 'برای رنگ روغن هم مناسب است؟',
    askedAt: '2026-05-02T10:00:00Z',
    answer: { author: 'پشتیبانی بوژان', body: 'بله.', answeredAt: '2026-05-02T12:00:00Z' },
  },
  { id: 'q-2', author: 'مهدی', question: 'جنس دسته چوبی است؟', askedAt: '2026-05-03T10:00:00Z' },
];

function renderTabs(over: Partial<Parameters<typeof ProductTabs>[0]> = {}) {
  return render(
    <ProductTabs
      slug="p-06"
      description="ست ۶ عددی قلم‌مو سرگرد."
      specs={[
        { label: 'برند', value: 'بوژان استودیو' },
        { label: 'دسته‌بندی', value: 'ابزار هنری' },
      ]}
      reviews={reviews}
      breakdown={breakdown}
      questions={questions}
      shipping={{
        deliveryEstimate: '۲ تا ۵ روز کاری',
        returnWindowDays: 7,
        freeShippingLabel: 'روی همه‌ی سفارش‌ها',
      }}
      {...over}
    />,
  );
}

const visiblePanels = () =>
  screen
    .getAllByRole('tabpanel', { hidden: true })
    .filter((panel) => !panel.hasAttribute('hidden'));

describe('the product tabs', () => {
  it('opens on the description with the other panels hidden but present', () => {
    renderTabs();

    const shown = visiblePanels();
    expect(shown).toHaveLength(1);
    expect(shown[0]).toHaveAttribute('id', 'product-panel-about');

    // Present, not rendered — this is the crawler's copy of the page.
    expect(screen.getAllByRole('tabpanel', { hidden: true })).toHaveLength(5);
    expect(screen.getByText('مشخصات محصول', { selector: 'h2' })).toBeInTheDocument();
  });

  it.each([
    ['مشخصات محصول', 'product-panel-specs'],
    ['نظرات کاربران', 'product-panel-reviews'],
    ['پرسش و پاسخ', 'product-panel-questions'],
    ['ارسال و مرجوعی', 'product-panel-shipping'],
  ])('shows only %s when its tab is chosen', async (label, id) => {
    const user = userEvent.setup();
    renderTabs();

    await user.click(screen.getByRole('tab', { name: new RegExp(label) }));

    const shown = visiblePanels();
    expect(shown).toHaveLength(1);
    expect(shown[0]).toHaveAttribute('id', id);
  });

  it('puts the reviews and the questions on their own tabs', async () => {
    const user = userEvent.setup();
    renderTabs();

    await user.click(screen.getByRole('tab', { name: /نظرات کاربران/ }));
    const panel = screen.getByRole('tabpanel', { name: /نظرات کاربران/ });
    expect(within(panel).getByText('نگار')).toBeInTheDocument();
    expect(within(panel).getByText('خرید تاییدشده')).toBeInTheDocument();

    await user.click(screen.getByRole('tab', { name: /پرسش و پاسخ/ }));
    const answers = screen.getByRole('tabpanel', { name: /پرسش و پاسخ/ });
    expect(within(answers).getByText('برای رنگ روغن هم مناسب است؟')).toBeInTheDocument();
    // The unanswered one says so rather than looking like a question nobody asked.
    expect(within(answers).getByText('در انتظار پاسخ')).toBeInTheDocument();
  });

  it('drops the score breakdown when nothing has been rated', async () => {
    const user = userEvent.setup();
    renderTabs({
      reviews: [],
      breakdown: { average: 0, total: 0, counts: { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0 } },
    });

    await user.click(screen.getByRole('tab', { name: /نظرات کاربران/ }));
    const panel = screen.getByRole('tabpanel', { name: /نظرات کاربران/ });

    expect(within(panel).getByText('هنوز نظری ثبت نشده')).toBeInTheDocument();
    // A five-bar chart of zeros beside that sentence says the same nothing twice.
    expect(within(panel).queryByText('از ۰ نظر')).not.toBeInTheDocument();
  });

  it('leaves out the free-shipping promise when the shop makes none', async () => {
    const user = userEvent.setup();
    renderTabs({
      shipping: { deliveryEstimate: '۲ تا ۵ روز کاری', returnWindowDays: 7, freeShippingLabel: null },
    });

    await user.click(screen.getByRole('tab', { name: /ارسال و مرجوعی/ }));
    const panel = screen.getByRole('tabpanel', { name: /ارسال و مرجوعی/ });

    expect(within(panel).getByText('مهلت مرجوعی')).toBeInTheDocument();
    expect(within(panel).queryByText('ارسال رایگان')).not.toBeInTheDocument();
  });

  it('moves between tabs with the arrow keys, left being next in an RTL strip', async () => {
    const user = userEvent.setup();
    renderTabs();

    await user.click(screen.getByRole('tab', { name: /درباره محصول/ }));
    await user.keyboard('{ArrowLeft}');

    expect(screen.getByRole('tab', { name: /مشخصات محصول/ })).toHaveAttribute(
      'aria-selected',
      'true',
    );

    await user.keyboard('{ArrowRight}');
    expect(screen.getByRole('tab', { name: /درباره محصول/ })).toHaveAttribute(
      'aria-selected',
      'true',
    );
  });
});
