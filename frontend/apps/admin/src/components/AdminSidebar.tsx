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
        'border-outline-variant bg-surface fixed inset-y-0 start-0 z-50 hidden flex-col border-e transition-[width] duration-200 md:flex',
        collapsed ? 'px-xs py-md w-20' : 'px-sm py-md w-64',
      )}
    >
      {/*
        Brand, then a rule. The header used to be separated from the navigation
        by 40px of nothing, which on a 10-group menu was the difference between
        seeing the last group and scrolling for it; a hairline says the same
        thing in 1px and says it even when the list is scrolled.
      */}
      <div
        className={cn(
          'gap-sm border-outline-variant/60 pb-md flex items-center border-b',
          collapsed ? 'flex-col px-0' : 'px-sm',
        )}
      >
        <span className="bg-primary-container font-headline text-card-title text-on-primary flex h-10 w-10 shrink-0 items-center justify-center rounded-lg">
          ب
        </span>
        {!collapsed && (
          <div className="min-w-0">
            <BrandLogo
              src={process.env.NEXT_PUBLIC_BRAND_LOGO}
              wordmark="بوژان"
              height={24}
              wordmarkClassName="font-headline text-card-title leading-tight text-primary"
            />
            <p className="text-helper text-on-surface-variant">پنل مدیریت</p>
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

      {/*
        `space-y-xs`, not `space-y-lg`: the gaps used to separate six blocks of
        open links, and now they separate ten one-line headings, where 24px of
        air between each reads as ten unrelated menus rather than one.
      */}
      <div className="hide-scrollbar mt-sm space-y-xs flex-1 overflow-y-auto">
        <AdminNavList collapsed={collapsed} />
      </div>
    </nav>
  );
}
