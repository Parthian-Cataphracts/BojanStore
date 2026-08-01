'use client';

import { useState } from 'react';
import { Button } from '@bojan/ui';

/**
 * Download and share controls from screen 34.
 *
 * "Download" prints to PDF via the browser rather than shipping a PDF library —
 * swap for the backend's generated invoice once that endpoint exists.
 */
export function InvoiceActions({ orderNumber }: { orderNumber: string }) {
  const [shared, setShared] = useState<string | null>(null);

  async function share() {
    const url = window.location.href;
    const title = `فاکتور سفارش ${orderNumber}`;

    if (navigator.share) {
      try {
        await navigator.share({ title, url });
        return;
      } catch {
        // User dismissed the sheet — fall through to copying instead.
      }
    }

    try {
      await navigator.clipboard.writeText(url);
      setShared('لینک فاکتور کپی شد.');
    } catch {
      setShared('کپی لینک ممکن نشد.');
    }
  }

  return (
    <div className="flex flex-col gap-sm">
      <div className="flex flex-col gap-md sm:flex-row">
        <Button icon="download" size="lg" fullWidth onClick={() => window.print()}>
          دانلود فاکتور
        </Button>
        <Button icon="share" variant="outline" size="lg" fullWidth onClick={share}>
          اشتراک‌گذاری
        </Button>
      </div>

      {shared && (
        <p aria-live="polite" className="text-center text-caption text-primary">
          {shared}
        </p>
      )}
    </div>
  );
}
