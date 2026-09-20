'use client';

import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { Icon, cn } from '@bojan/ui';
import { helpForPath } from '@/lib/help-content';

/**
 * Top-bar "?" button that opens a side panel with the plain-language guide for
 * the screen the operator is on. The full index lives at /help.
 */
export function HelpButton() {
  const [open, setOpen] = useState(false);
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);
  const pathname = usePathname() ?? '/';
  const entry = helpForPath(pathname);

  return (
    <>
      <button
        type="button"
        aria-label="راهنما"
        title="راهنمای این صفحه"
        onClick={() => setOpen(true)}
        className="text-on-surface-variant hover:bg-surface-container hover:text-secondary flex h-10 w-10 items-center justify-center rounded-full transition-colors"
      >
        <Icon name="help" />
      </button>

      {open &&
        mounted &&
        createPortal(
        <div className="fixed inset-0 z-[60] flex" role="dialog" aria-modal="true" aria-label="راهنما">
          <div className="flex-1 bg-black/40" onClick={() => setOpen(false)} />
          <aside className="bg-surface flex h-full w-full max-w-md flex-col overflow-y-auto shadow-xl">
            <div className="border-outline-variant bg-surface px-lg sticky top-0 flex items-center justify-between border-b py-4">
              <span className="text-title-md text-primary flex items-center gap-2 font-bold">
                <Icon name="help" />
                راهنمای این صفحه
              </span>
              <button
                type="button"
                aria-label="بستن"
                onClick={() => setOpen(false)}
                className="text-on-surface-variant hover:bg-surface-container flex h-9 w-9 items-center justify-center rounded-full"
              >
                <Icon name="close" />
              </button>
            </div>

            <div className="px-lg flex-1 py-5">
              {entry ? (
                <>
                  <h2 className="text-title-md text-on-surface font-bold">{entry.title}</h2>
                  <p className="text-body-md text-on-surface-variant mt-2 leading-7">{entry.what}</p>

                  <h3 className="text-label-lg text-on-surface mt-5 font-bold">قدم‌به‌قدم</h3>
                  <ol className="text-body-md text-on-surface-variant mt-2 list-decimal space-y-2 ps-5 leading-7">
                    {entry.steps.map((s, i) => (
                      <li key={i}>{s}</li>
                    ))}
                  </ol>

                  {entry.caution && (
                    <p
                      className={cn(
                        'text-body-md mt-5 flex items-start gap-2 rounded-lg p-3 leading-7',
                        'bg-error-container text-on-error-container',
                      )}
                    >
                      <Icon name="warning" className="mt-0.5 shrink-0" />
                      <span>{entry.caution}</span>
                    </p>
                  )}
                </>
              ) : (
                <p className="text-body-md text-on-surface-variant">
                  برای این صفحه راهنمای اختصاصی ثبت نشده. فهرست کامل راهنماها را ببینید.
                </p>
              )}

              <Link
                href="/help"
                onClick={() => setOpen(false)}
                className="bg-primary text-on-primary text-label-lg mt-6 inline-flex rounded-full px-4 py-2 font-bold hover:opacity-90"
              >
                دیدن همهٔ راهنماها
              </Link>
            </div>
          </aside>
        </div>,
          document.body,
        )}
    </>
  );
}
