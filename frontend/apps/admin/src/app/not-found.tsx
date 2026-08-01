import Link from 'next/link';
import { EmptyState, buttonClasses } from '@bojan/ui';

/**
 * The panel had no not-found page, so a bad id — a deleted order, a mistyped
 * URL — fell through to Next's unstyled default, outside the panel entirely.
 */
export default function NotFound() {
  return (
    <main className="flex min-h-screen items-center justify-center p-lg">
      <EmptyState
        icon="search_off"
        title="این صفحه پیدا نشد"
        description="ممکن است رکورد حذف شده باشد یا نشانی اشتباه باشد."
        action={
          <Link href="/" className={buttonClasses()}>
            بازگشت به داشبورد
          </Link>
        }
      />
    </main>
  );
}
