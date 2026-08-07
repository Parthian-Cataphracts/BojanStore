import { BrandLogo, IconButton, cn } from '@bojan/ui';
import { AdminNavList } from './AdminNavList';

/**
 * Fixed sidebar pinned to the start edge (the right, in RTL), per screen 92.
 * Hidden below `md`, where `AdminDrawer` and `AdminBottomNav` take over.
 *
 * `collapsed` shrinks it to an icon-only rail — the toggle and the
 * expanded/collapsed width live in `AdminShell`, which also owns the
 * content margin that has to track this same width.
 *
 * Logical properties throughout — `start-0` and `border-e` render exactly as
 * the physical `right-0` / `border-l` they replace, without assuming direction.
 */
export function AdminSidebar({
  collapsed,
  onToggle,
}: {
  collapsed: boolean;
  onToggle: () => void;
}) {
  return (
    <nav
      aria-label="ناوبری پنل مدیریت"
      className={cn(
        'fixed inset-y-0 start-0 z-50 hidden flex-col border-e border-outline-variant bg-surface py-lg transition-[width] duration-200 md:flex',
        collapsed ? 'w-20 px-xs' : 'w-64 px-md',
      )}
    >
      <div className={cn('mb-xl flex items-center gap-md', collapsed ? 'flex-col px-0' : 'px-sm')}>
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-primary-container font-headline text-display-md text-on-primary">
          ب
        </span>
        {!collapsed && (
          <div className="min-w-0">
            <BrandLogo
              src={process.env.NEXT_PUBLIC_BRAND_LOGO}
              wordmark="بوژان"
              height={28}
              wordmarkClassName="font-headline text-display-md leading-tight text-primary"
            />
            <p className="text-caption text-on-surface-variant">پنل مدیریت</p>
          </div>
        )}

        <IconButton
          icon={collapsed ? 'chevron_left' : 'chevron_right'}
          label={collapsed ? 'باز کردن منو' : 'جمع کردن منو'}
          variant="plain"
          size={20}
          onClick={onToggle}
          className={collapsed ? '' : 'ms-auto'}
        />
      </div>

      <div className="hide-scrollbar flex-1 space-y-lg overflow-y-auto">
        <AdminNavList collapsed={collapsed} />
      </div>
    </nav>
  );
}
