import type { Metadata } from 'next';
import { Suspense } from 'react';
import { AdminOtpForm } from '@/components/AdminOtpForm';

export const metadata: Metadata = { title: 'ورود با رمز یک‌بار مصرف' };

/**
 * The destination of screen 91's OTP link.
 *
 * The design draws the entry point but stops there; this is the step it leads
 * to, built from screen 91's own card so the two read as one flow.
 */
export default function AdminOtpPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-paper-bg p-lg">
      <Suspense fallback={<div className="h-[480px] w-full max-w-md rounded-xl bg-surface-container" />}>
        <AdminOtpForm />
      </Suspense>
    </div>
  );
}
