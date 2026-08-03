import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Container } from '@/components/layout/Container';
import { PaymentCallback } from './PaymentCallback';

export const metadata: Metadata = {
  title: 'تأیید پرداخت',
  robots: { index: false },
};

/**
 * Where the payment gateway returns the shopper — the address in
 * <c>Payment:ReturnUrl</c>.
 *
 * This route did not exist, so every gateway return landed on a 404. The work
 * happens in the client component, which needs the query string; the Suspense
 * boundary is what `useSearchParams` requires to keep the rest of the page
 * static.
 */
export default function PaymentCallbackPage() {
  return (
    <Container className="py-lg md:py-xl">
      <Suspense fallback={null}>
        <PaymentCallback />
      </Suspense>
    </Container>
  );
}
