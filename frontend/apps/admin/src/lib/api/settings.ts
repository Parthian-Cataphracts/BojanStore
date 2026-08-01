import { mockAdminUsers, mockAuditLog } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { ApiKeyDto, AuditEntryDto, AdminUserDto, Paged, SettingsSectionDto } from './types';

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
