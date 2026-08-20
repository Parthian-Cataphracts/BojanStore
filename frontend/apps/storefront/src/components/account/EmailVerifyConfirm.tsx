'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { useEffect, useState } from 'react';
import { Button, Icon } from '@bojan/ui';
import { routes } from '@/lib/routes';
import { AuthCard } from '../auth/AuthCard';

type Status = 'checking' | 'ok' | 'failed';

/**
 * The landing page for the link `EmailLinks.VerifyEmail` puts in the
 * verification e-mail. The token proves identity here — see the note on the
 * proxy route — so this needs no session and shows one of exactly two
 * outcomes: every failure the backend can report (unknown, expired,
 * already-used, or the address changed since) reads the same to a customer,
 * "این لینک نامعتبر یا منقضی شده است."
 */
export function EmailVerifyConfirm() {
  const searchParams = useSearchParams();
  const token = searchParams.get('token');
  const [status, setStatus] = useState<Status>('checking');

  useEffect(() => {
    if (!token) {
      setStatus('failed');
      return;
    }

    let cancelled = false;

    fetch(`/api/account/email/verify?token=${encodeURIComponent(token)}`)
      .then((response) => {
        if (!cancelled) setStatus(response.ok ? 'ok' : 'failed');
      })
      .catch(() => {
        if (!cancelled) setStatus('failed');
      });

    return () => {
      cancelled = true;
    };
  }, [token]);

  if (status === 'checking') {
    return (
      <AuthCard icon="mail" title="در حال تایید ایمیل">
        <p className="flex items-center justify-center gap-xs text-body-md text-on-surface-variant">
          <Icon name="progress_activity" size={20} className="animate-spin" />
          کمی صبر کنید...
        </p>
      </AuthCard>
    );
  }

  if (status === 'ok') {
    return (
      <AuthCard icon="check_circle" title="ایمیل شما با موفقیت تایید شد">
        <Link href={routes.profile}>
          <Button type="button" size="lg" fullWidth>
            بازگشت به اطلاعات شخصی
          </Button>
        </Link>
      </AuthCard>
    );
  }

  return (
    <AuthCard icon="error" tone="error" title="این لینک نامعتبر یا منقضی شده است">
      <div className="flex flex-col gap-sm">
        <p className="text-center text-body-md text-on-surface-variant">
          از بخش اطلاعات شخصی می‌توانید دوباره لینک تایید بخواهید.
        </p>
        <Link href={routes.profile}>
          <Button type="button" size="lg" fullWidth variant="outline">
            بازگشت به اطلاعات شخصی
          </Button>
        </Link>
      </div>
    </AuthCard>
  );
}
