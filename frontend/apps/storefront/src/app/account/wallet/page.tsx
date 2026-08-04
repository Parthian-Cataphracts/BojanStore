import type { Metadata } from 'next';
import { Badge, Card, Icon, buttonClasses, cn, formatDateTime, formatPrice } from '@bojan/ui';
import { Container } from '@/components/layout/Container';
import { PageHeader } from '@/components/layout/PageHeader';
import { WalletTopUpForm } from '@/components/account/WalletTopUpForm';
import { getWallet, getWalletTransactions } from '@/lib/api/activity';
import { routes } from '@/lib/routes';

export const metadata: Metadata = {
  title: 'کیف پول و اعتبار خرید',
  robots: { index: false },
};

/** Screen 58 — Wallet and store credit. */
export default async function WalletPage() {
  // The balance comes from the wallet read rather than the profile, so the
  // figure on screen and the limits the form validates against are the same
  // response — they cannot disagree.
  const [wallet, transactions] = await Promise.all([getWallet(), getWalletTransactions()]);

  return (
    <Container className="flex flex-col gap-lg py-lg md:py-xl">
      <PageHeader title="کیف پول و اعتبار خرید" backHref={routes.account} />

      {/* Balance */}
      <Card className="relative overflow-hidden p-xl shadow-soft">
        {/* Decorative blob from the design; purely visual. */}
        <span
          aria-hidden="true"
          className="absolute -end-10 -top-10 h-40 w-40 rounded-full bg-primary-fixed-dim/25 blur-2xl"
        />

        <div className="relative flex flex-col gap-md">
          <span className="text-caption text-on-surface-variant">اعتبار قابل استفاده</span>
          <strong className="tabular font-headline text-headline-lg-mobile text-primary md:text-headline-lg">
            {formatPrice(wallet.balance)}
          </strong>

          <div className="mt-sm flex flex-col gap-md sm:flex-row sm:items-start">
            <a
              href="#topup"
              className={buttonClasses({ fullWidth: true, className: 'gap-sm' })}
            >
              <Icon name="add" size={20} />
              افزایش اعتبار
            </a>
            <a
              href="#transactions"
              className={buttonClasses({ variant: 'outline', fullWidth: true, className: 'gap-sm' })}
            >
              <Icon name="receipt_long" size={20} />
              مشاهده تراکنش‌ها
            </a>
          </div>
        </div>
      </Card>

      {/*
        Requests that have been filed but not yet decided. Shown above the
        ledger rather than inside it: these are not money the shopper has, and
        the distinction is the whole point of the flow.
      */}
      {wallet.pendingTopUps.length > 0 && (
        <section className="flex flex-col gap-sm">
          <h2 className="font-headline text-display-md text-primary">در انتظار تأیید</h2>
          <Card className="divide-y divide-paper-border">
            {wallet.pendingTopUps.map((topUp) => (
              <div key={topUp.id} className="flex items-center gap-md p-md">
                <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-surface-container-high text-on-surface-variant">
                  <Icon name="hourglass_top" size={22} />
                </span>
                <div className="flex min-w-0 flex-1 flex-col gap-xs">
                  <span className="text-body-md text-on-surface">
                    {topUp.method === 'manual' ? 'واریز کارت به کارت' : 'پرداخت آنلاین'}
                  </span>
                  <span className="tabular text-caption text-outline">
                    {topUp.trackingNumber
                      ? `شماره پیگیری ${topUp.trackingNumber}`
                      : formatDateTime(topUp.createdAt)}
                  </span>
                </div>
                <div className="flex shrink-0 flex-col items-end gap-xs">
                  <span className="tabular text-body-md font-label-md text-on-surface-variant">
                    {formatPrice(topUp.amount)}
                  </span>
                  <Badge tone="warning">در انتظار</Badge>
                </div>
              </div>
            ))}
          </Card>
          <p className="text-caption leading-relaxed text-outline">
            این مبالغ تا زمان تأیید، قابل استفاده نیستند.
          </p>
        </section>
      )}

      <section id="topup">
        <WalletTopUpForm wallet={wallet} />
      </section>

      {/* History */}
      <section id="transactions" className="flex flex-col gap-md">
        <h2 className="font-headline text-display-md text-primary">تراکنش‌های اخیر</h2>

        <Card className="divide-y divide-paper-border">
          {transactions.map((transaction) => {
            const credit = transaction.amount > 0;
            return (
              <div key={transaction.id} className="flex items-center gap-md p-md">
                <span
                  className={cn(
                    'flex h-11 w-11 shrink-0 items-center justify-center rounded-full',
                    credit ? 'bg-soft-mint text-primary' : 'bg-surface-container-high text-on-surface-variant',
                  )}
                >
                  <Icon name={transaction.icon} size={22} />
                </span>

                <div className="flex min-w-0 flex-1 flex-col gap-xs">
                  <span className="text-body-md text-on-surface">{transaction.title}</span>
                  <span className="tabular text-caption text-outline">
                    {formatDateTime(transaction.createdAt)}
                  </span>
                </div>

                <div className="flex shrink-0 flex-col items-end gap-xs">
                  <span
                    className={cn(
                      'tabular text-body-md font-label-md',
                      credit ? 'text-primary' : 'text-on-surface-variant',
                    )}
                  >
                    {credit ? '+' : '−'} {formatPrice(Math.abs(transaction.amount))}
                  </span>
                  <Badge tone={transaction.status === 'success' ? 'mint' : 'warning'}>
                    {transaction.status === 'success' ? 'موفق' : 'در انتظار'}
                  </Badge>
                </div>
              </div>
            );
          })}
        </Card>
      </section>
    </Container>
  );
}
