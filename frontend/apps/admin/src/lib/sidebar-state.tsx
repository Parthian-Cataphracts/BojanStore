'use client';

import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';

const STORAGE_KEY = 'bojan-admin-sidebar-collapsed';

interface SidebarState {
  collapsed: boolean;
  toggle: () => void;
}

const SidebarContext = createContext<SidebarState>({ collapsed: false, toggle: () => {} });

/**
 * Shares the sidebar's collapsed/expanded state with anything that has to
 * track its width — `AdminSidebar` itself, `AdminShell`'s content margin, and
 * `AdminTopBar`, which every page renders independently and which otherwise
 * has no way to know the rail beside it just changed width.
 */
export function SidebarStateProvider({ children }: { children: ReactNode }) {
  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    try {
      setCollapsed(window.localStorage.getItem(STORAGE_KEY) === '1');
    } catch {
      // No saved preference — the sidebar just starts expanded.
    }
  }, []);

  function toggle() {
    setCollapsed((current) => {
      const next = !current;
      try {
        window.localStorage.setItem(STORAGE_KEY, next ? '1' : '0');
      } catch {
        // Persistence is a convenience, not a requirement — the toggle still works this session.
      }
      return next;
    });
  }

  return <SidebarContext.Provider value={{ collapsed, toggle }}>{children}</SidebarContext.Provider>;
}

export function useSidebarState(): SidebarState {
  return useContext(SidebarContext);
}
