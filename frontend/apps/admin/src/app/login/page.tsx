import type { Metadata } from 'next';
import { Suspense } from 'react';
import { AdminLoginForm } from '@/components/AdminLoginForm';

export const metadata: Metadata = { title: 'ورود به پنل مدیریت' };

/**
 * Screen 91 — Admin sign-in. Rendered outside the panel chrome.
 *
 * The form reads `?next=` so it can return the operator to the screen the
 * middleware turned them away from; that needs a boundary to stay static.
 */
export default function AdminLoginPage() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-paper-bg p-lg">
      <Suspense fallback={<div className="h-[520px] w-full max-w-md rounded-xl bg-surface-container" />}>
        <AdminLoginForm />
      </Suspense>
    </div>
  );
}
