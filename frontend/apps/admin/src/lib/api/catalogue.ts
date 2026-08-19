import { mockAdminProducts, mockBrands, mockCategories, mockCollections } from '@/lib/mock';
import { api, useMockData } from './client';
import type {
  AdminBrandDto,
  AdminCategoryDto,
  AdminCollectionDto,
  AdminProductDto,
  CatalogueOptionDto,
  Paged,
} from './types';

/** Pickers for the product/category/brand forms — screens 97-110. */
export async function getBrands(): Promise<CatalogueOptionDto[]> {
  if (useMockData) return mockBrands.map((brand) => ({ slug: brand.slug, name: brand.name }));
  const { items } = await api.get<Paged<AdminBrandDto>>('/brands', {
    query: { pageSize: 200 },
    auth: true,
  });
  return items.map((brand) => ({ slug: brand.slug, name: brand.name }));
}

/**
 * Every category, children included.
 *
 * The picker used to drop anything with a parent, which meant a product could
 * only ever be filed under a top-level category — the leaves of the tree the
 * storefront actually browses by were not offered at all.
 *
 * A child is shown under its parent's name, because two parents may each have
 * a child called «آبرنگ» and a list of bare names gives the operator no way to
 * tell which one they are ticking.
 */
export async function getCategories(): Promise<CatalogueOptionDto[]> {
  if (useMockData) {
    return mockCategories.map((category) => ({
      slug: category.slug,
      name: category.parent ? `${category.parent} › ${category.name}` : category.name,
    }));
  }
  const { items } = await api.get<Paged<AdminCategoryDto>>('/categories', {
    query: { pageSize: 200 },
    auth: true,
  });
  return items.map((category) => ({
    slug: category.slug,
    name: category.parentName ? `${category.parentName} › ${category.name}` : category.name,
  }));
}

/**
 * The catalogue as picker options, for the collection's products panel.
 *
 * Capped at the page size the API allows rather than paged: the panel filters
 * this list in the browser, and a collection is a curated grouping an editor
 * assembles by hand — a shop large enough for the cap to bite needs a picker
 * that searches on the server, which is a different control from this one.
 */
export async function getProductOptions(): Promise<CatalogueOptionDto[]> {
  if (useMockData) {
    // The fixtures carry no slug, and an id is the other thing
    // `POST /collections/products` resolves — so mock mode picks the one it
    // has rather than inventing the one it does not.
    return mockAdminProducts.map((product) => ({ slug: product.id, name: product.title }));
  }
  const { items } = await api.get<Paged<AdminProductDto>>('/products', {
    query: { pageSize: 200 },
    auth: true,
  });
  return items.map((product) => ({ slug: product.slug ?? product.id, name: product.title }));
}

/** The curated groupings a product can be put into, for the product form. */
export async function getCollections(): Promise<CatalogueOptionDto[]> {
  if (useMockData) {
    return mockCollections.map((collection) => ({
      slug: collection.slug,
      name: collection.name,
    }));
  }
  const { items } = await api.get<Paged<AdminCollectionDto>>('/collections', {
    query: { pageSize: 200 },
    auth: true,
  });
  return items.map((collection) => ({ slug: collection.slug, name: collection.title }));
}
