import { mockAdminUsers, mockAuditLog } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type {
  ApiKeyDto,
  AuditEntryDto,
  AdminUserDto,
  BackupJobDto,
  Paged,
  RolePermissionDto,
  ServerStatusDto,
  ServiceHealthDto,
  SettingsSectionDto,
} from './types';

/** Screen 146's saved grants. Empty in mock mode — nothing to fabricate for a permission grid. */
export async function getRolePermissions(): Promise<RolePermissionDto[]> {
  if (useMockData) return [];

  return api.get<RolePermissionDto[]>('/roles/permissions', { auth: true }).catch(() => []);
}

/**
 * Screen 156's table. No mock fallback with invented rows — a backup job is
 * either a real one this API queued and ran, or the list is empty; a fixture
 * here would claim history that never happened.
 */
export async function getBackups(): Promise<BackupJobDto[]> {
  if (useMockData) return [];

  return api.get<BackupJobDto[]>('/backups', { auth: true }).catch(() => []);
}

export interface ListAuditQuery {
  q?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export async function getAuditLog(query: ListAuditQuery = {}): Promise<Paged<AuditEntryDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const q = (query.q ?? '').trim();
    const matched = mockAuditLog.filter(
      (entry) => !q || entry.actor.includes(q) || entry.action.includes(q) || entry.target.includes(q),
    );
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<AuditEntryDto>>('/settings/audit', {
    query: { q: query.q, from: query.from, to: query.to, page, pageSize },
    auth: true,
  });
}

export interface ListAdminUsersQuery {
  q?: string;
  page?: number;
  pageSize?: number;
}

export async function getAdminUsers(query: ListAdminUsersQuery = {}): Promise<Paged<AdminUserDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const q = (query.q ?? '').trim();
    const matched = mockAdminUsers.filter(
      (user) => !q || user.name.includes(q) || user.email.includes(q),
    );
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<AdminUserDto>>('/settings/users', {
    query: { q: query.q, page, pageSize },
    auth: true,
  });
}

/** No dedicated fixture — the panel has never issued a key in mock mode. */
export async function getApiKeys(): Promise<ApiKeyDto[]> {
  if (useMockData) return [];
  return api.get<ApiKeyDto[]>('/settings/api-keys', { auth: true });
}

export async function getSettingsSection(section: string): Promise<SettingsSectionDto> {
  if (useMockData) return {};
  return api.get<SettingsSectionDto>(`/settings/${section}`, { auth: true });
}

/**
 * Screen 157 — the health of each dependency the API actually checks.
 *
 * No fixture. The screen used to list four invented services with invented
 * latencies and a fixed "last checked", which an owner could read as a healthy
 * system while nothing was being monitored at all. In mock mode there is
 * nothing to check, so the screen shows an empty board and says so.
 */
export async function getSystemHealth(): Promise<ServiceHealthDto[]> {
  if (useMockData) return [];
  return api.get<ServiceHealthDto[]>('/system/health', { auth: true });
}

/**
 * Dashboard's server-status card. No mock fallback — there is no process to
 * describe in mock mode, so the card is skipped there rather than shown with
 * fabricated CPU and memory numbers.
 */
export async function getServerStatus(): Promise<ServerStatusDto | null> {
  if (useMockData) return null;
  return api.get<ServerStatusDto>('/system/status', { auth: true }).catch(() => null);
}
