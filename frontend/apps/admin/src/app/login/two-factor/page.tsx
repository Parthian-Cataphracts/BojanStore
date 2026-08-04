import type { Metadata } from 'next';
import { Suspense } from 'react';
import { AdminTwoFactorForm } from '@/components/AdminTwoFactorForm';

export const metadata: Metadata = { title: 'تایید دو مرحله‌ای' };

/**
 * The step screen 91 hands off to when the account has a second factor.
 *
 * Public only in the sense that the middleware lets it render: without the
 * challenge cookie the password step sets, the route behind it refuses every
 * code, so there is nothing to reach by visiting this address directly.
 */
export default function AdminTwoFactorPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-paper-bg p-lg">
      <Suspense
        fallback={<div className="h-[420px] w-full max-w-md rounded-xl bg-surface-container" />}
      >
        <AdminTwoFactorForm />
      </Suspense>
    </div>
  );
}
