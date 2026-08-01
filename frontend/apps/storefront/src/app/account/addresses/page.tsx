import type { Metadata } from 'next';
import Link from 'next/link';
import { Badge, Card, EmptyState, Icon, buttonClasses, toPersianDigits } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { DeleteAddressButton } from '@/components/account/DeleteAddressButton';
import { getAddresses } from '@/lib/api/account';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'آدرس‌های من',
  robots: { index: false },
};

/** Screen 14 — My addresses. */
export default async function AddressesPage() {
  const addresses = await getAddresses();

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="آدرس‌های من" backHref={routes.account} />

      {addresses.length > 0 ? (
        <ul className="flex flex-col gap-md">
          {addresses.map((address) => (
            <li key={address.id}>
              <Card className="flex flex-col gap-sm p-lg">
                <div className="flex flex-wrap items-start justify-between gap-sm">
                  <div className="flex min-w-0 flex-col gap-xs">
                    <span className="flex flex-wrap items-center gap-sm">
                      <span className="text-label-md font-label-md text-primary">
                        {address.recipient}
                      </span>
                      {address.isDefault && <Badge tone="mint">پیش‌فرض</Badge>}
                    </span>
                    <span className="tabular text-caption text-on-surface-variant">
                      {toPersianDigits(address.phone)}
                    </span>
                  </div>

                  <div className="flex shrink-0 items-center gap-xs">
                    <Link
                      href={`${routes.addresses}/${address.id}`}
                      aria-label={`ویرایش آدرس ${address.title}`}
                      className="rounded p-xs text-on-surface-variant transition-colors hover:bg-surface-container hover:text-primary"
                    >
                      <Icon name="edit" size={20} />
                    </Link>
                    <DeleteAddressButton addressId={address.id} title={address.title} />
                  </div>
                </div>

                <p className="text-body-md leading-relaxed text-on-surface">
                  {address.province}، {address.city}، {address.line}
                </p>

                <span className="flex items-center gap-xs text-caption text-on-surface-variant">
                  <Icon name="markunread_mailbox" size={16} />
                  <span className="tabular">کد پستی: {toPersianDigits(address.postalCode)}</span>
                </span>
              </Card>
            </li>
          ))}
        </ul>
      ) : (
        <EmptyState
          icon="location_off"
          title="هنوز آدرسی ثبت نکرده‌اید"
          description="برای ثبت سفارش حداقل یک آدرس تحویل لازم است."
        />
      )}

      <Link
        href={`${routes.addresses}/new`}
        className={buttonClasses({ size: 'lg', fullWidth: true, className: 'gap-sm md:w-auto md:self-start' })}
      >
        <Icon name="add" size={20} />
        افزودن آدرس جدید
      </Link>
    </Container>
  );
}
