import { BrandLogo } from '@bojan/ui';
import { AdminNavList } from './AdminNavList';

/**
 * Fixed 256px sidebar pinned to the start edge (the right, in RTL), per screen
 * 92. Hidden below `md`, where `AdminDrawer` and `AdminBottomNav` take over.
 *
 * Logical properties throughout — `start-0` and `border-e` render exactly as
 * the physical `right-0` / `border-l` they replace, without assuming direction.
 */
export function AdminSidebar() {
  return (
    <nav
      aria-label="ناوبری پنل مدیریت"
      className="fixed inset-y-0 start-0 z-50 hidden w-64 flex-col border-e border-outline-variant bg-surface px-md py-lg md:flex"
    >
      <div className="mb-xl flex items-center gap-md px-sm">
        <span className="flex h-12 w-12 items-center justify-center rounded-lg bg-primary-container font-headline text-display-md text-on-primary">
          ب
        </span>
        <div>
          <BrandLogo
            wordmark="بوژان"
            height={28}
            wordmarkClassName="font-headline text-display-md leading-tight text-primary"
          />
          <p className="text-caption text-on-surface-variant">پنل مدیریت</p>
        </div>
      </div>

      <div className="hide-scrollbar flex-1 space-y-lg overflow-y-auto">
        <AdminNavList />
      </div>
    </nav>
  );
}
