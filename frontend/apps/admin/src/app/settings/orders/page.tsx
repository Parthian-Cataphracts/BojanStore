import type { Metadata } from 'next';
import { AdminPage } from '@/components/AdminPage';
import { SettingsForm } from '@/components/SettingsForm';
import { getSettingsSection } from '@/lib/api/settings';
import { withSavedValues } from '@/lib/settings-fields';

export const metadata: Metadata = { title: 'تنظیمات سفارش و لغو' };

/**
 * Order rules the shop sets rather than the code — currently the cancellation
 * penalty.
 *
 * It is a commercial decision that changes without a deploy, which is why it is
 * a setting at all. Unset means zero: a shop that has never opened this screen
 * does not quietly start charging people. The percentage only ever applies from
 * the warehouse step onward, and never when the shop itself cancelled — both of
 * those are rules in the domain, not options here.
 */
export default async function Page() {
  const settings = await getSettingsSection('orders');

  return (
    <AdminPage
      title="تنظیمات سفارش و لغو"
      description="قوانین لغو سفارش و درصد جریمه‌ای که هنگام لغو کسر می‌شود."
      breadcrumbs={[
        { label: 'داشبورد', href: '/' },
        { label: 'تنظیمات', href: '/settings' },
        { label: 'سفارش و لغو' },
      ]}
    >
      <SettingsForm
        section="orders"
        sections={[
          {
            title: 'جریمه لغو سفارش',
            icon: 'cancel',
            fields: withSavedValues(
              [
                {
                  name: 'cancellationPenaltyPercent',
                  label: 'درصد جریمه لغو سفارش',
                  kind: 'text',
                  value: '0',
                  suffix: '٪',
                  latin: true,
                },
              ],
              settings,
            ),
          },
        ]}
      />

      <section className="mt-lg flex flex-col gap-sm rounded-lg bg-surface-container-low p-lg">
        <h2 className="font-headline text-card-title text-primary">این درصد کجا اعمال می‌شود؟</h2>

        <p className="text-body-md leading-relaxed text-on-surface-variant">
          مسیر سفارش این‌گونه است: پرداخت، تایید اولیه، آماده‌سازی در انبار، ارسال، تحویل.
        </p>

        <ul className="flex list-disc flex-col gap-xs ps-lg text-body-md leading-relaxed text-on-surface-variant">
          <li>
            تا پیش از انبار (در انتظار پرداخت و در حال آماده‌سازی) هنوز هزینه‌ای صرف سفارش نشده، پس
            جریمه‌ای کسر نمی‌شود و کل مبلغ کیف پول بازمی‌گردد.
          </li>
          <li>
            از مرحله انبار به بعد (بسته‌بندی و ارسال) کار انجام‌شده با بازگشت کالا برنمی‌گردد، پس این
            درصد از مبلغ بازگشتی کسر می‌شود.
          </li>
          <li>
            اگر لغو به‌دلیل فروشگاه باشد، اپراتور در صفحه سفارش گزینه «به‌درخواست مشتری» را برمی‌دارد و
            هیچ جریمه‌ای کسر نمی‌شود.
          </li>
          <li>
            موجودی تا مرحله انبار خودکار به انبار بازمی‌گردد؛ اگر سفارش ارسال شده باشد، پس از دریافت
            مرسوله باید از صفحه «موجودی و انبار» دستی ثبت شود.
          </li>
        </ul>
      </section>
    </AdminPage>
  );
}
