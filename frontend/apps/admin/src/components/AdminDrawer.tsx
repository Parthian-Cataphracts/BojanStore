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
      className="ms-0 me-auto max-w-xs md:hidden"
      footer={
        <button
          type="button"
          onClick={onSignOut}
          disabled={signingOut}
          className="flex w-full items-center justify-center gap-sm rounded-lg border border-error/40 px-lg py-md text-label-md font-label-md text-error transition-colors hover:bg-error-container disabled:opacity-50"
        >
          <Icon name="logout" size={20} />
          خروج از پنل
        </button>
      }
    >
      <div className="space-y-lg">
        <AdminNavList onNavigate={onClose} />
      </div>
    </Sheet>
  );
}
