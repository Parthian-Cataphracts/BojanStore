import { mockAdminCustomers, mockAdminOrders, mockAdminProducts, mockSupportThreads } from '@/lib/mock';
import { api, useMockData } from './client';
import type { DashboardKpisDto } from './types';

export async function getDashboardKpis(): Promise<DashboardKpisDto> {
  if (useMockData) {
    const today = new Date().toDateString();
    const ordersToday = mockAdminOrders.filter((o) => new Date(o.placedAt).toDateString() === today);
    return {
      revenueToday: ordersToday.reduce((sum, o) => sum + o.total, 0),
      revenueThisMonth: mockAdminOrders.reduce((sum, o) => sum + o.total, 0),
      ordersToday: ordersToday.length,
      ordersThisMonth: mockAdminOrders.length,
      pendingOrders: mockAdminOrders.filter((o) => o.status === 'pending' || o.status === 'processing').length,
      lowStockProducts: mockAdminProducts.filter((p) => p.stock <= 10).length,
      newCustomersThisMonth: mockAdminCustomers.length,
      openSupportThreads: mockSupportThreads.filter((t) => t.status === 'open').length,
    };
  }

  return api.get<DashboardKpisDto>('/dashboard', { auth: true });
}
