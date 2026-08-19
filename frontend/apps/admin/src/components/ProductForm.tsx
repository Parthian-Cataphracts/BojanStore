'use client';

import { useRouter } from 'next/navigation';
import { useState, type ChangeEvent, type FormEvent } from 'react';
import { Button, Checkbox, FormStatus, Icon, Input, Select, Textarea, cn, normalizeDigitsInput } from '@bojan/ui';
import { FormLayout, FormSection } from './FormLayout';
import { ProductTools } from './product/ProductTools';
import { SlugPicker } from './SlugPicker';
import { postJson } from '@/lib/submit';
import type { AdminProductDto, CatalogueOptionDto } from '@/lib/api/types';

type Errors = Partial<Record<'title' | 'sku' | 'price' | 'stock' | 'categories' | 'form', string>>;
type ProductStatus = 'published' | 'draft' | 'archived';

/**
 * Screens 97, 98 and 105-110 — the product editor.
 *
 * The design splits pricing, SKU, variants, images and discounts across
 * separate screens; here they are sections of one form, because saving a
 * product in five round-trips would be worse for the operator, not better.
 *
 * The brand select and the two pickers submit catalogue *slugs* — the same
 * values `POST /products` resolves against — not the display names shown in
 * the lists.
 */
export function ProductForm({
  product,
  brands,
  categories,
  collections,
}: {
  product?: AdminProductDto;
  brands: CatalogueOptionDto[];
  categories: CatalogueOptionDto[];
  collections: CatalogueOptionDto[];
}) {
  const router = useRouter();
  const isEdit = Boolean(product);
  const [status, setStatus] = useState<ProductStatus>((product?.status as ProductStatus) ?? 'draft');
  // The whole gallery when the API returned one; the primary alone otherwise.
  const [images, setImages] = useState<string[]>(
    product?.images ?? (product?.image ? [product.image] : []),
  );
  /*
    Categories and collections are React state rather than form fields: both
    are sets the operator builds by ticking, and a `<select multiple>` — the
    only thing FormData can carry a set in — loses the whole selection to a
    stray click. The first category is the primary one, which is why the order
    of this array matters and is preserved as picked.

    `categorySlugs` falls back to the single `categorySlug` for a product read
    back from an older response, so opening one never shows it filed nowhere
    and then clears it on save.
  */
  const [pickedCategories, setPickedCategories] = useState<string[]>(
    product?.categorySlugs ?? (product?.categorySlug ? [product.categorySlug] : []),
  );
  const [pickedCollections, setPickedCollections] = useState<string[]>(
    product?.collectionSlugs ?? [],
  );
  const [uploading, setUploading] = useState(false);
  const [imageError, setImageError] = useState<string | null>(null);
  const [errors, setErrors] = useState<Errors>({});
  const error = errors.form;
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  /**
   * Uploads what was picked and appends the URLs the API issued.
   *
   * The picker had no handler at all, so images could be removed here but never
   * added — the only way in was screen 105. Sequential rather than parallel:
   * the upload route is rate limited per operator, and a burst of a dozen would
   * spend that window at once.
   */
  async function addImages(event: ChangeEvent<HTMLInputElement>) {
    const files = [...(event.target.files ?? [])];
    event.target.value = '';
    if (files.length === 0) return;

    setImageError(null);
    setUploading(true);
    try {
      const uploaded: string[] = [];
      for (const file of files) {
        const body = new FormData();
        body.append('file', file);

        const response = await fetch('/api/upload/products', { method: 'POST', body });
        const result = (await response.json().catch(() => null)) as
          | { url?: string; error?: string }
          | null;

        if (!response.ok || !result?.url) {
          throw new Error(result?.error ?? 'بارگذاری تصویر انجام نشد.');
        }
        uploaded.push(result.url);
      }

      setImages((current) => [...current, ...uploaded]);
    } catch (cause) {
      setImageError(cause instanceof Error ? cause.message : 'بارگذاری تصویر انجام نشد.');
    } finally {
      setUploading(false);
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const next: Errors = {};

    const title = String(data.get('title') ?? '').trim();
    const sku = String(data.get('sku') ?? '').trim();
    const price = Number(normalizeDigitsInput(String(data.get('price') ?? '')));
    const stock = Number(normalizeDigitsInput(String(data.get('stock') ?? '')));

    if (title.length < 3) next.title = 'نام محصول را کامل وارد کنید.';
    // Underscore included alongside the hyphen: the seeder's own SKUs
    // (`BZ-P_01`) use it, and the API places no charset restriction on this
    // field beyond a length cap — "English, at least 4 characters" is a rule
    // this form invented on its own. Without underscore here, opening any
    // seeded product and saving it — without even touching the SKU field —
    // failed this check on the value the product already had.
    if (!/^[A-Za-z0-9_-]{4,}$/.test(sku)) next.sku = 'کد کالا باید انگلیسی و حداقل ۴ نویسه باشد.';
    if (!Number.isFinite(price) || price <= 0) next.price = 'قیمت را وارد کنید.';
    if (!Number.isFinite(stock) || stock < 0) next.stock = 'موجودی نمی‌تواند منفی باشد.';
    // The API refuses an empty set too — a product filed nowhere is one that
    // shows up in no listing at all. Caught here so the operator is told which
    // field to fix instead of being handed the server's word for it.
    if (pickedCategories.length === 0) next.categories = 'دست‌کم یک دسته‌بندی انتخاب کنید.';

    setErrors(next);
    if (Object.keys(next).length > 0) return;

    setSaving(true);
    setSaved(false);
    try {
      await postJson('/api/admin/products', {
        ...(product ? { id: product.id } : null),
        title,
        sku,
        price,
        stock,
        status,
        images,
        brand: String(data.get('brand') ?? ''),
        categories: pickedCategories,
        collections: pickedCollections,
        description: String(data.get('description') ?? ''),
        compareAt: Number(normalizeDigitsInput(String(data.get('compareAt') ?? ''))) || null,
        costPrice: Number(normalizeDigitsInput(String(data.get('costPrice') ?? ''))) || null,
        lowStock: Number(normalizeDigitsInput(String(data.get('lowStock') ?? ''))) || null,
        trackStock: data.get('trackStock') === 'on',
        backorder: data.get('backorder') === 'on',
        metaTitle: String(data.get('metaTitle') ?? ''),
        metaDescription: String(data.get('metaDescription') ?? ''),
        slug: String(data.get('slug') ?? ''),
      });
      setSaved(true);
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ذخیره محصول انجام نشد.' });
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate>
      <FormLayout
        aside={
          <>
            <FormSection title="انتشار" icon="publish">
              <div className="flex flex-col gap-sm">
                {(['published', 'draft', 'archived'] as const).map((option) => (
                  <button
                    key={option}
                    type="button"
                    onClick={() => setStatus(option)}
                    className={cn(
                      'flex items-center gap-sm rounded-lg border p-sm text-start transition-colors',
                      status === option
                        ? 'border-primary bg-soft-mint/40'
                        : 'border-outline-variant hover:bg-surface-container-low',
                    )}
                  >
                    <Icon
                      name={status === option ? 'radio_button_checked' : 'radio_button_unchecked'}
                      size={20}
                      className={status === option ? 'text-primary' : 'text-outline-variant'}
                    />
                    <span className="text-body-md text-on-surface">
                      {{ published: 'منتشر شده', draft: 'پیش‌نویس', archived: 'بایگانی' }[option]}
                    </span>
                  </button>
                ))}
              </div>
            </FormSection>

            <FormSection title="تصاویر" icon="photo_library" description="اولین تصویر، تصویر شاخص است.">
              {images.length > 0 && (
                <div className="flex flex-wrap gap-sm">
                  {images.map((src, index) => (
                    <span
                      key={src}
                      className="relative h-20 w-20 overflow-hidden rounded-lg border border-outline-variant"
                    >
                      {/* eslint-disable-next-line @next/next/no-img-element */}
                      <img src={src} alt="" className="h-full w-full object-cover" />
                      {index === 0 && (
                        <span className="absolute inset-x-0 bottom-0 bg-inverse-surface/70 py-px text-center text-[10px] text-inverse-on-surface">
                          شاخص
                        </span>
                      )}
                      <button
                        type="button"
                        aria-label="حذف تصویر"
                        onClick={() => setImages((current) => current.filter((item) => item !== src))}
                        className="absolute end-1 top-1 flex h-5 w-5 items-center justify-center rounded-full bg-inverse-surface/70 text-inverse-on-surface"
                      >
                        <Icon name="close" size={14} />
                      </button>
                    </span>
                  ))}
                </div>
              )}

              <label className="flex cursor-pointer flex-col items-center gap-xs rounded-lg border border-dashed border-outline-variant p-md text-center transition-colors hover:bg-surface-container-low">
                <Icon
                  name={uploading ? 'progress_activity' : 'add_photo_alternate'}
                  size={24}
                  className="text-primary"
                />
                <span className="text-caption font-medium text-primary">
                  {uploading ? 'در حال بارگذاری…' : 'افزودن تصویر'}
                </span>
                {/* The formats the API sniffs for — "image/*" would offer the
                    operator files it will refuse. */}
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  multiple
                  disabled={uploading}
                  onChange={addImages}
                  className="sr-only"
                />
              </label>

              <FormStatus error={imageError} />
            </FormSection>
          </>
        }
        actions={
          <>
            <Button type="submit" size="lg" loading={saving} className="px-xl">
              {isEdit ? 'ذخیره تغییرات' : 'ثبت محصول'}
            </Button>
            <Button type="button" variant="ghost" size="lg" className="px-xl" onClick={() => router.back()}>
              انصراف
            </Button>
            <FormStatus error={error} />
            <FormStatus ok={saved ? 'تغییرات ذخیره شد.' : null} />
          </>
        }
      >
        <FormSection title="اطلاعات پایه" icon="inventory_2">
          <Input
            name="title"
            label="نام محصول"
            defaultValue={product?.title ?? ''}
            required
            {...(errors.title ? { error: errors.title } : null)}
          />

          <div className="grid gap-lg md:grid-cols-2">
            <Input
              name="sku"
              label="کد کالا (SKU)"
              placeholder="BZ-PLN-A5-001"
              className="latin"
              defaultValue={product?.sku ?? ''}
              required
              {...(errors.sku ? { error: errors.sku } : null)}
            />
            <Select name="brand" label="برند" defaultValue={product?.brandSlug ?? ''}>
              <option value="">انتخاب کنید</option>
              {brands.map((brand) => (
                <option key={brand.slug} value={brand.slug}>
                  {brand.name}
                </option>
              ))}
            </Select>
          </div>

          {/*
            A product can sit in more than one place in the shop's own tree —
            a notebook that is both stationery and a gift — and filing it under
            one meant the other listing did not have it. The first pick is the
            primary: the category the breadcrumb walks up and the product card
            names.
          */}
          <SlugPicker
            label="دسته‌بندی‌ها"
            hint="اولین دسته‌بندی، دسته‌بندی اصلی محصول است."
            options={categories}
            value={pickedCategories}
            onChange={setPickedCategories}
            placeholder="انتخاب کنید"
            primaryHint="اصلی"
            {...(errors.categories ? { error: errors.categories } : null)}
          />

          {/*
            Collections were only editable from the collection's side — and
            there is no screen there for it either, so a grouping could be
            created in the panel and never filled. Empty is a perfectly
            ordinary answer here, unlike categories.
          */}
          <SlugPicker
            label="کالکشن‌ها"
            hint="محصول می‌تواند در چند کالکشن باشد یا در هیچ‌کدام."
            options={collections}
            value={pickedCollections}
            onChange={setPickedCollections}
            placeholder="انتخاب کنید (اختیاری)"
          />

          <Textarea name="description" label="توضیحات" rows={5} defaultValue={product?.description ?? ''} />
        </FormSection>

        <FormSection title="قیمت‌گذاری" icon="payments" description="قیمت‌ها به تومان وارد می‌شوند.">
          <div className="grid gap-lg md:grid-cols-3">
            <Input
              name="price"
              label="قیمت فروش"
              inputMode="numeric"
              suffix="تومان"
              defaultValue={product?.price ?? ''}
              required
              {...(errors.price ? { error: errors.price } : null)}
            />
            <Input
              name="compareAt"
              label="قیمت پیش از تخفیف"
              inputMode="numeric"
              suffix="تومان"
              hint="اختیاری"
              defaultValue={product?.compareAt ?? ''}
            />
            <Input
              name="costPrice"
              label="قیمت تمام‌شده"
              inputMode="numeric"
              suffix="تومان"
              defaultValue={product?.costPrice ?? ''}
            />
          </div>
        </FormSection>

        <FormSection title="موجودی و انبار" icon="warehouse">
          <div className="grid gap-lg md:grid-cols-2">
            <Input
              name="stock"
              label="موجودی"
              inputMode="numeric"
              defaultValue={product?.stock ?? 0}
              required
              {...(errors.stock ? { error: errors.stock } : null)}
            />
            <Input
              name="lowStock"
              label="آستانه هشدار کمبود"
              inputMode="numeric"
              defaultValue={product?.lowStock ?? 5}
            />
          </div>

          {/*
            Both of these change what the checkout does — an untracked product
            is never short, and a backorder one may be sold past its count — so
            they have to render the stored state rather than a fixed default,
            or opening a saved product would silently offer to reset them.
          */}
          <Checkbox
            name="trackStock"
            defaultChecked={product?.trackStock ?? true}
            label="موجودی این محصول پیگیری شود."
          />
          <Checkbox
            name="backorder"
            defaultChecked={product?.backorder ?? false}
            label="فروش در حالت ناموجود مجاز باشد."
          />
        </FormSection>

        {/*
          The panel has six screens for one product's detail and this used to
          link exactly one of them — the other five were addressable by URL and
          reachable from nowhere. They all live in ProductTools now.

          Only once the product exists, because every one of them is addressed
          by id.
        */}
        {isEdit && product ? (
          <FormSection
            title="ویرایش تخصصی"
            icon="widgets"
            description="هر کدام از این‌ها صفحه‌ی خودش را دارد."
          >
            <ProductTools productId={product.id} />
          </FormSection>
        ) : null}

        <FormSection title="سئو" icon="travel_explore">
          <Input name="metaTitle" label="عنوان متا" defaultValue={product?.metaTitle ?? ''} />
          <Textarea
            name="metaDescription"
            label="توضیحات متا"
            rows={2}
            defaultValue={product?.metaDescription ?? ''}
          />
          <Input
            name="slug"
            label="نشانی صفحه"
            className="latin"
            placeholder="daily-planner-a5"
            defaultValue={product?.slug ?? ''}
          />
        </FormSection>
      </FormLayout>
    </form>
  );
}
