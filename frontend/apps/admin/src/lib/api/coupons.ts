import { mockAdminCoupons } from '@/lib/mock';
import { api, useMockData } from './client';
import { DEFAULT_PAGE_SIZE, paginate } from './paginate';
import type { AdminCouponDto, Paged } from './types';

export interface ListCouponsQuery {
  q?: string;
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getCoupons(query: ListCouponsQuery = {}): Promise<Paged<AdminCouponDto>> {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? DEFAULT_PAGE_SIZE;

  if (useMockData) {
    const q = (query.q ?? '').trim();
    const matched = mockAdminCoupons.filter((coupon) => {
      const matchesStatus =
        !query.status || (query.status === 'active' ? coupon.active : !coupon.active);
      const matchesQuery = !q || coupon.code.includes(q) || coupon.title.includes(q);
      return matchesStatus && matchesQuery;
    });
    return paginate(matched, page, pageSize);
  }

  return api.get<Paged<AdminCouponDto>>('/coupons', {
    query: { q: query.q, status: query.status, page, pageSize },
    auth: true,
  });
}

export async function getCoupon(id: string): Promise<AdminCouponDto | null> {
  if (useMockData) {
    return mockAdminCoupons.find((coupon) => coupon.id === id) ?? null;
  }

  try {
    return await api.get<AdminCouponDto>(`/coupons/${id}`, { auth: true });
  } catch {
    return null;
  }
}
