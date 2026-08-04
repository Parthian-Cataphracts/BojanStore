'use client';

import { Button } from '@bojan/ui';

/**
 * Hands the page to the browser's print dialog.
 *
 * Printing rather than generating a PDF server-side is deliberate: the document
 * is already laid out for A4 (see the print rules in `@bojan/config`), and
 * "save as PDF" is a button in the same dialog — so a PDF library would ship a
 * second renderer whose output could disagree with the one on screen.
 */
export function PrintButton({ label = 'چاپ / ذخیره PDF' }: { label?: string }) {
  return (
    <Button icon="print" variant="outline" size="sm" onClick={() => window.print()}>
      {label}
    </Button>
  );
}
