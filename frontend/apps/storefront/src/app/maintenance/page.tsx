import type { Metadata } from 'next';
import { StatusScreen } from '@/components/status/StatusScreen';

export const metadata: Metadata = {
  title: 'در حال تعمیر و نگهداری',
  robots: { index: false, follow: false },
};

/**
 * Shown in place of every page while "حالت تعمیر و نگهداری" (screen 150) is
 * on — middleware rewrites here instead of linking to it, so the address bar
 * still shows the page the visitor asked for.
 */
export default function MaintenancePage() {
  return (
    <StatusScreen
      icon="engineering"
      tone="neutral"
      title="فروشگاه موقتاً در دسترس نیست"
      message="در حال انجام تعمیر و نگهداری هستیم. لطفاً چند دقیقه دیگر دوباره سر بزنید."
      note="در صورت نیاز فوری می‌توانید با پشتیبانی تماس بگیرید."
    />
  );
}
