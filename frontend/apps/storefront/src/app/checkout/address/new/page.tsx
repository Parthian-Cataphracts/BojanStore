import type { Metadata } from 'next';
import { CheckoutShell } from '@/components/checkout/CheckoutShell';
import { AddressForm } from '@/components/account/AddressForm';

export const metadata: Metadata = {
  title: 'افزودن آدرس جدید',
  robots: { index: false },
};

/** Screen 72 — Add an address inside the checkout flow. */
export default function CheckoutNewAddressPage() {
  return (
    <CheckoutShell
      step="shipping"
      title="افزودن آدرس جدید"
      description="لطفاً اطلاعات گیرنده و آدرس دقیق را وارد کنید."
    >
      <AddressForm />
    </CheckoutShell>
  );
}
