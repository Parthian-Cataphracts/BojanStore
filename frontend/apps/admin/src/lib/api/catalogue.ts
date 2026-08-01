import { mockBrands, mockCategories } from '@/lib/mock';
import { api, useMockData } from './client';
import type { AdminBrandDto, AdminCategoryDto, CatalogueOptionDto, Paged } from './types';

/** Pickers for the product/category/brand forms — screens 97-110. */
export async function getBrands(): Promise<CatalogueOptionDto[]> {
  if (useMockData) return mockBrands.map((brand) => ({ slug: brand.slug, name: brand.name }));
  const { items } = await api.get<Paged<AdminBrandDto>>('/brands', {
    query: { pageSize: 200 },
    auth: true,
  });
  return items.map((brand) => ({ slug: brand.slug, name: brand.name }));
}

export async function getCategories(): Promise<CatalogueOptionDto[]> {
  if (useMockData) {
    return mockCategories
      .filter((category) => !category.parent)
      .map((category) => ({ slug: category.slug, name: category.name }));
  }
  const { items } = await api.get<Paged<AdminCategoryDto>>('/categories', {
    query: { pageSize: 200 },
    auth: true,
  });
  return items.map((category) => ({ slug: category.slug, name: category.name }));
}
