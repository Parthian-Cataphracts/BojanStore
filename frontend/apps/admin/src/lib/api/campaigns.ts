import { mockCampaigns } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { CampaignDto, Paged } from './types';

export interface ListCampaignsQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getCampaigns(query: ListCampaignsQuery = {}): Promise<Paged<CampaignDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const q = (query.q ?? '').trim();
    const matched = mockCampaigns.filter((campaign) => {
      const matchesStatus = !query.status || campaign.status === query.status;
      const matchesQuery = !q || campaign.title.includes(q);
      return matchesStatus && matchesQuery;
    });
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<CampaignDto>>('/campaigns', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

export async function getCampaign(id: string): Promise<CampaignDto | null> {
  if (useMockData) {
    return mockCampaigns.find((campaign) => campaign.id === id) ?? null;
  }

  try {
    return await api.get<CampaignDto>(`/campaigns/${id}`, { auth: true });
  } catch {
    return null;
  }
}
