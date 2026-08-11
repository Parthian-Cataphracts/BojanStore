'use client';

/**
 * The last resort: an error thrown by the root layout itself.
 *
 * `error.tsx` next door handles a failure inside a page, and the shared layout
 * is still standing when it renders. This one replaces the layout, so it has to
 * bring its own `<html>` and `<body>` and cannot use a single component, font
 * or token from the design system — anything imported from there is code that
 * has already been shown capable of throwing on this render.
 *
 * Without it Next.js shows its own fallback, which reads "Application error: a
 * client-side exception has occurred": English, in a Persian panel, and phrased
 * for whoever wrote it rather than the operator whose morning it just
 * interrupted.
 *
 * Everything here is inline: styles, text, and the two actions worth offering.
 */
export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html lang="fa" dir="rtl">
      <body
        style={{
          margin: 0,
          minHeight: '100vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: '24px',
          background: '#FBF7F0',
          color: '#003441',
          fontFamily: 'Vazirmatn, Tahoma, system-ui, sans-serif',
          lineHeight: 1.8,
        }}
      >
        <main style={{ maxWidth: '32rem', textAlign: 'center' }}>
          <h1 style={{ fontSize: '1.5rem', margin: '0 0 12px' }}>مشکلی پیش آمد</h1>

          <p style={{ margin: '0 0 24px', color: '#4A5A5F' }}>
            پنل به‌درستی بارگذاری نشد. اگر در حال ذخیره‌ی چیزی بودید، پیش از تلاش دوباره بررسی
            کنید که ذخیره شده یا نه، تا دوباره ثبت نشود.
          </p>

          <div
            style={{
              display: 'flex',
              flexWrap: 'wrap',
              gap: '12px',
              justifyContent: 'center',
              marginBottom: '24px',
            }}
          >
            <button
              type="button"
              onClick={reset}
              style={{
                border: 0,
                borderRadius: '999px',
                padding: '12px 32px',
                background: '#003441',
                color: '#FFFFFF',
                fontSize: '1rem',
                fontFamily: 'inherit',
                cursor: 'pointer',
              }}
            >
              تلاش دوباره
            </button>

            {/*
              A plain anchor, not next/link, and deliberately: the router is part
              of what just failed, so a client-side navigation would be asking
              the broken thing to rescue itself. This forces a full reload.
            */}
            {/* eslint-disable-next-line @next/next/no-html-link-for-pages */}
            <a
              href="/"
              style={{
                borderRadius: '999px',
                padding: '12px 32px',
                border: '1px solid #003441',
                color: '#003441',
                fontSize: '1rem',
                textDecoration: 'none',
              }}
            >
              داشبورد
            </a>
          </div>

          {/*
            The reference support needs to find this in the logs. Without it the
            report is "it didn't work", which is not something anyone can look up.
          */}
          {error.digest && (
            <p style={{ margin: 0, fontSize: '0.8rem', color: '#7A8A8F' }}>
              کد پیگیری برای پشتیبانی:{' '}
              <code style={{ direction: 'ltr', display: 'inline-block' }}>{error.digest}</code>
            </p>
          )}
        </main>
      </body>
    </html>
  );
}
