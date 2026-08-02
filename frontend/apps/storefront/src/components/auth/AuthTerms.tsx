import Link from 'next/link';
import { routes } from '@/lib/routes';

/** The one line both doors carry, so the wording cannot drift between them. */
export function AuthTerms() {
  return (
    <p className="mt-md text-center text-caption leading-relaxed text-on-surface-variant md:mt-lg">
      با ورود یا ثبت‌نام،{' '}
      <Link href={routes.terms} className="text-primary underline">
        قوانین و مقررات
      </Link>{' '}
      و{' '}
      <Link href={routes.privacy} className="text-primary underline">
        حریم خصوصی
      </Link>{' '}
      بوژان را می‌پذیرید.
    </p>
  );
}
