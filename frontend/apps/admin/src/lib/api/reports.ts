import {
  mockAdminCustomers,
  mockAdminOrders,
  mockAdminProducts,
  mockCampaigns,
} from '@/lib/mock';
import { api, useMockData } from './client';
import type {
  CampaignPerformanceDto,
  CustomerGrowthPointDto,
  FinancialTotalsDto,
  SalesPointDto,
  StatusCountDto,
  StockLevelsDto,
  TopProductDto,
} from './types';

export interface ReportRange {
  from?: string;
  to?: string;
  grouping?: string;
}

export async function getSalesReport(range: ReportRange = {}): Promise<SalesPointDto[]> {
  if (useMockData) {
    return mockAdminOrders.map((order) => ({
      period: order.placedAt,
      revenue: order.total,
      orders: 1,
    }));
  }

  return api.get<SalesPointDto[]>('/reports/sales', {
    query: { from: range.from, to: range.to, grouping: range.grouping },
    auth: true,
  });
}

export async function getOrderStatusCounts(range: ReportRange = {}): Promise<StatusCountDto[]> {
  if (useMockData) {
    const counts = new Map<string, number>();
    for (const order of mockAdminOrders) counts.set(order.status, (counts.get(order.status) ?? 0) + 1);
    return [...counts.entries()].map(([status, count]) => ({ status, count }));
  }

  return api.get<StatusCountDto[]>('/reports/order-status', {
    query: { from: range.from, to: range.to },
    auth: true,
  });
}

export async function getTopProducts(range: ReportRange & { limit?: number } = {}): Promise<TopProductDto[]> {
  if (useMockData) {
    return mockAdminProducts.slice(0, range.limit ?? 10).map((product) => ({
      productId: product.id,
      title: product.title,
      sku: product.sku,
      unitsSold: product.stock,
      revenue: product.price * product.stock,
    }));
  }

  return api.get<TopProductDto[]>('/reports/top-products', {
    query: { from: range.from, to: range.to, limit: range.limit },
    auth: true,
  });
}

export async function getCustomerGrowth(range: ReportRange = {}): Promise<CustomerGrowthPointDto[]> {
  if (useMockData) {
    return mockAdminCustomers.map((customer) => ({
      period: customer.joinedAt,
      newCustomers: 1,
      returningCustomers: 0,
    }));
  }

  return api.get<CustomerGrowthPointDto[]>('/reports/customer-growth', {
    query: { from: range.from, to: range.to, grouping: range.grouping },
    auth: true,
  });
}

export async function getStockLevels(): Promise<StockLevelsDto> {
  if (useMockData) {
    return {
      inStock: mockAdminProducts.filter((p) => p.stock > 10).length,
      lowStock: mockAdminProducts.filter((p) => p.stock > 0 && p.stock <= 10).length,
      outOfStock: mockAdminProducts.filter((p) => p.stock === 0).length,
      inventoryValue: mockAdminProducts.reduce((sum, p) => sum + p.costPrice * p.stock, 0),
    };
  }

  return api.get<StockLevelsDto>('/reports/stock-levels', { auth: true });
}

export async function getCampaignPerformance(range: ReportRange = {}): Promise<CampaignPerformanceDto[]> {
  if (useMockData) {
    return mockCampaigns.map((campaign) => ({
      campaignId: campaign.id,
      title: campaign.title,
      reach: campaign.reach,
      conversion: campaign.conversion,
      conversionRate: campaign.reach > 0 ? campaign.conversion / campaign.reach : 0,
    }));
  }

  return api.get<CampaignPerformanceDto[]>('/reports/campaigns', {
    query: { from: range.from, to: range.to },
    auth: true,
  });
}

export async function getFinancialTotals(range: ReportRange = {}): Promise<FinancialTotalsDto> {
  if (useMockData) {
    const grossRevenue = mockAdminOrders.reduce((sum, o) => sum + o.total, 0);
    const costOfGoods = mockAdminOrders.reduce(
      (sum, o) => sum + o.items.reduce((s, i) => s + i.unitPrice * i.quantity * 0.62, 0),
      0,
    );
    return {
      grossRevenue,
      discounts: 0,
      shipping: 0,
      netRevenue: grossRevenue,
      costOfGoods: Math.round(costOfGoods),
      grossProfit: Math.round(grossRevenue - costOfGoods),
      orderCount: mockAdminOrders.length,
    };
  }

  return api.get<FinancialTotalsDto>('/reports/financial', {
    query: { from: range.from, to: range.to },
    auth: true,
  });
}
