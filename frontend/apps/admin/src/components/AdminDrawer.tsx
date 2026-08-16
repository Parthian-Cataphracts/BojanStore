'use client';

import { Icon, Sheet } from '@bojan/ui';
import { AdminNavList } from './AdminNavList';

/**
 * Mobile navigation drawer.
 *
 * The phone drawings put a drawer button in the top bar but only four
 * destinations in the bottom tab bar; everything else in `adminNav` lives here.
 * It slides in from the start edge — the same side the sidebar occupies on
 * desktop, and the side the button that opens it sits on.
 */
export function AdminDrawer({
  open,
  onClose,
  onSignOut,
  signingOut,
}: {
  open: boolean;
  onClose: () => void;
  onSignOut: () => void;
  signingOut: boolean;
}) {
  return (
    <Sheet
      open={open}
      onClose={onClose}
      placement="side"
      title="پنل مدیریت بوژان"
      // `Sheet` defaults its side drawer to the end edge; the panel opens from
      // the start, where both the sidebar and the drawer button are.
      className="me-auto ms-0 max-w-xs md:hidden"
      footer={
        <button
          type="button"
          onClick={onSignOut}
          disabled={signingOut}
          className="gap-sm border-error/40 px-lg py-md text-label-md font-label-md text-error hover:bg-error-container flex w-full items-center justify-center rounded-lg border transition-colors disabled:opacity-50"
        >
          <Icon name="logout" size={20} />
          خروج از پنل
        </button>
      }
    >
      {/* Same rhythm as the desktop sidebar — the drawer shows the same ten
          collapsed headings, so it wants the same gap between them. */}
      <div className="space-y-xs">
        <AdminNavList onNavigate={onClose} />
      </div>
    </Sheet>
  );
}
