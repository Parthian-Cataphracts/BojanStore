import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SettingsForm } from '@/components/SettingsForm';
import { TrustSealsForm } from '@/components/TrustSealsForm';
import { getSettingsSection } from '@/lib/api/settings';
import { withSavedValues } from '@/lib/settings-fields';
import { parseTrustSeals, TRUST_SEALS_KEY } from '@/lib/trust-seals';

export const metadata: Metadata = { title: 'تنظیمات فروشگاه' };

/**
 * Screen 141 — تنظیمات فروشگاه.
 *
 * Everything on this screen is read by the storefront through
 * `GET /store/settings`. It used to be four fields that nothing outside the
 * panel looked at: the shop's name, address, phone and support e-mail were all
 * written into the storefront's components, so an owner who moved office or
 * changed their number edited the settings screen, saw it save, and the site
 * went on showing the old ones.
 *
 * The delivery and returns figures are here for the same reason. They were
 * quoted on the product page, in the cart, on the policy pages and in the FAQ —
 * five places, five copies, and no two of them had to agree.
 */
export default async function Page() {
  const settings = await getSettingsSection('store');

  return (
    <AdminPage
      title="تنظیمات فروشگاه"
      description="نام، اطلاعات تماس، شبکه‌های اجتماعی و وعده‌هایی که در سایت به مشتری داده می‌شود."
      breadcrumbs={[{ label: 'داشبورد', href: '/' }, { label: 'تنظیمات', href: '/settings' }, { label: 'فروشگاه' }]}
    >
      <SettingsForm section="store"
        sections={[
          { title: 'اطلاعات فروشگاه', icon: 'storefront', fields: withSavedValues([
            { name: 'storeName', label: 'نام فروشگاه', kind: 'text', value: 'بوژان',
              hint: 'در هدر، فوتر و عنوان صفحه‌ها نمایش داده می‌شود.' },
            { name: 'tagline', label: 'شعار', kind: 'text', value: 'برای لحظه‌های خلاق زندگی' },
            { name: 'description', label: 'معرفی کوتاه', kind: 'textarea',
              hint: 'یک یا دو جمله برای فوتر و توضیح صفحه‌های عمومی.' },
          ], settings) },

          { title: 'راه‌های تماس', icon: 'call', fields: withSavedValues([
            { name: 'phone', label: 'تلفن پشتیبانی', kind: 'text', value: '۰۲۱-۱۲۳۴۵۶۷۸' },
            { name: 'email', label: 'ایمیل پشتیبانی', kind: 'text', value: 'info@bojan.com', latin: true },
            { name: 'workingHours', label: 'ساعت پاسخگویی', kind: 'text',
              placeholder: 'شنبه تا چهارشنبه، ۹ تا ۱۷' },
            { name: 'businessPhone', label: 'تلفن فروش سازمانی', kind: 'text',
              hint: 'خالی بماند: همان تلفن پشتیبانی نمایش داده می‌شود.' },
            { name: 'businessEmail', label: 'ایمیل فروش سازمانی', kind: 'text', latin: true,
              hint: 'خالی بماند: همان ایمیل پشتیبانی نمایش داده می‌شود.' },
          ], settings) },

          { title: 'آدرس', icon: 'place', fields: withSavedValues([
            { name: 'address', label: 'آدرس فروشگاه', kind: 'textarea', value: 'تهران، خیابان ولیعصر، کوچه فرزان، پلاک ۱۲' },
            { name: 'postalCode', label: 'کد پستی', kind: 'text', value: '۱۹۶۸۸۴۳۵۶۱' },
          ], settings) },

          /*
            Only what the shop actually has. Each one is rendered in the footer
            only when it is filled in, so a shop with no LinkedIn has no LinkedIn
            icon rather than one that goes nowhere.
          */
          { title: 'شبکه‌های اجتماعی', icon: 'share', fields: withSavedValues([
            { name: 'instagram', label: 'اینستاگرام', kind: 'text', latin: true, placeholder: 'bojan.store' },
            { name: 'telegram', label: 'تلگرام', kind: 'text', latin: true, placeholder: 'bojanstore' },
            { name: 'whatsapp', label: 'واتس‌اپ', kind: 'text', latin: true, placeholder: '989120000000' },
            { name: 'linkedin', label: 'لینکدین', kind: 'text', latin: true },
          ], settings) },

          /*
            The figures the storefront quotes to a shopper.

            These are promises, not decoration: the threshold below decides what
            the cart says about free delivery, and the return window is what the
            product page tells a buyer before they buy. Both were written into
            the components in more than one place.
          */
          { title: 'وعده‌های فروشگاه', icon: 'handshake', fields: withSavedValues([
            /* Free delivery is not here. It belongs to each shipping method —
               «تنظیمات ← روش‌های ارسال» — because a courier that is never free
               and a post tier that is free over a million are both ordinary,
               and one figure for the whole shop could say neither. Keeping a
               copy here as well would be two places holding one rule.

               The unit is in the label rather than as a suffix: a number input
               draws its own stepper at the end of the field, which is where a
               suffix sits. */
            { name: 'returnWindowDays', label: 'مهلت مرجوعی (روز)', kind: 'number', value: '7',
              hint: 'در صفحه‌ی هر محصول و صفحه‌ی شرایط مرجوعی به مشتری گفته می‌شود.' },
            { name: 'deliveryEstimate', label: 'زمان تقریبی تحویل', kind: 'text', value: '۲ تا ۵ روز کاری',
              hint: 'روی صفحه‌ی محصول و صفحه‌ی ثبت سفارش نمایش داده می‌شود.' },
            { name: 'supportPromise', label: 'وعده‌ی پشتیبانی', kind: 'text',
              placeholder: 'پاسخگویی تا ۲۴ ساعت' },
          ], settings) },

          /*
            Only the switch that switches something.

            «خرید بدون ثبت‌نام» sat here reading on by default and the checkout
            has always required a signed-in customer — every route under `/me`
            derives the customer from a credential, and an order has a customer
            id it cannot do without. Guest checkout is a feature, not a setting,
            and a switch claiming it was already on told the owner their shop
            did something it does not do.
          */
          { title: 'عمومی', icon: 'tune', fields: withSavedValues([
            { name: 'maintenance', label: 'حالت تعمیر و نگهداری', kind: 'switch' },
          ], settings) },

          /*
            The editorial sections at the bottom of the home page.

            On by default, and each already disappears on its own when it has
            nothing to show — so these are not "hide it while it is empty".
            They are the owner saying they do not want the section at all, and
            they are switches rather than deletes: turning the magazine rail
            off must not unpublish articles the magazine page still serves, and
            clearing the testimonial rail by unticking every featured review is
            a decision that then has to be undone one review at a time.
          */
          { title: 'بخش‌های صفحه اصلی', icon: 'widgets', fields: withSavedValues([
            { name: 'homeTestimonials', label: 'نمایش «نظرات مشتریان»', kind: 'switch', checked: true },
            { name: 'homeArticles', label: 'نمایش «مطالب وبلاگ»', kind: 'switch', checked: true },
            { name: 'homeFaq', label: 'نمایش «سوالات متداول»', kind: 'switch', checked: true },
          ], settings) },
        ]}
      />

      {/*
        Its own form rather than a section of the one above. `SettingsForm`
        renders a fixed list of named fields, and this is a list whose length
        the owner decides — so it posts the same section under its own save
        button, and the marks are stored as one JSON row.
      */}
      <TrustSealsForm initial={parseTrustSeals(settings[TRUST_SEALS_KEY])} />
    </AdminPage>
  );
}
