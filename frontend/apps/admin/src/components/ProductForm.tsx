'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Badge, Button, Checkbox, Icon, Input, Select, Textarea, cn, normalizeDigitsInput } from '@bojan/ui';
import { FormLayout, FormSection } from './FormLayout';
import { postJson } from '@/lib/submit';
import type { AdminProductDto, CatalogueOptionDto } from '@/lib/api/types';

type Errors = Partial<Record<'title' | 'sku' | 'price' | 'stock' | 'form', string>>;
type ProductStatus = 'published' | 'draft' | 'archived';

/**
 * Screens 97, 98 and 105-110 — the product editor.
 *
 * The design splits pricing, SKU, variants, images and discounts across
 * separate screens; here they are sections of one form, because saving a
 * product in five round-trips would be worse for the operator, not better.
 *
 * Brand and category selects submit the catalogue *slug* — the same value
 * `POST /products` resolves against — not the display name shown in the list.
 */
export function ProductForm({
  product,
  brands,
  categories,
}: {
  product?: AdminProductDto;
  brands: CatalogueOptionDto[];
  categories: CatalogueOptionDto[];
}) {
  const router = useRouter();
  const isEdit = Boolean(product);
  const [status, setStatus] = useState<ProductStatus>((product?.status as ProductStatus) ?? 'draft');
  const [images, setImages] = useState<string[]>(product?.image ? [product.image] : []);
  const [errors, setErrors] = useState<Errors>({});
  const error = errors.form;
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const next: Errors = {};

    const title = String(data.get('title') ?? '').trim();
    const sku = String(data.get('sku') ?? '').trim();
    const price = Number(normalizeDigitsInput(String(data.get('price') ?? '')));
    const stock = Number(normalizeDigitsInput(String(data.get('stock') ?? '')));

    if (title.length < 3) next.title = 'نام محصول را کامل وارد کنید.';
    if (!/^[A-Za-z0-9-]{4,}$/.test(sku)) next.sku = 'کد کالا باید انگلیسی و حداقل ۴ نویسه باشد.';
    if (!Number.isFinite(price) || price <= 0) next.price = 'قیمت را وارد کنید.';
    if (!Number.isFinite(stock) || stock < 0) next.stock = 'موجودی نمی‌تواند منفی باشد.';

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
        category: String(data.get('category') ?? ''),
        description: String(data.get('description') ?? ''),
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
                <Icon name="add_photo_alternate" size={24} className="text-primary" />
                <span className="text-caption font-medium text-primary">افزودن تصویر</span>
                <input type="file" accept="image/*" multiple className="sr-only" />
              </label>
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
            {error && (
              <span role="alert" className="flex items-center gap-xs text-caption text-error">
                <Icon name="error" size={16} />
                {error}
              </span>
            )}
            {saved && (
              <span aria-live="polite" className="flex items-center gap-xs text-caption text-primary">
                <Icon name="check_circle" size={16} />
                تغییرات ذخیره شد.
              </span>
            )}
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

          <Select name="category" label="دسته‌بندی" defaultValue={product?.categorySlug ?? ''}>
            <option value="">انتخاب کنید</option>
            {categories.map((category) => (
              <option key={category.slug} value={category.slug}>
                {category.name}
              </option>
            ))}
          </Select>

          <Textarea name="description" label="توضیحات" rows={5} />
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
            <Input name="lowStock" label="آستانه هشدار کمبود" inputMode="numeric" defaultValue={10} />
          </div>

          <Checkbox name="trackStock" defaultChecked label="موجودی این محصول پیگیری شود." />
          <Checkbox name="backorder" label="فروش در حالت ناموجود مجاز باشد." />
        </FormSection>

        <FormSection title="تنوع محصول" icon="tune" description="رنگ، سایز و سایر ویژگی‌ها.">
          <div className="flex flex-wrap items-center gap-sm">
            <Badge tone="neutral">رنگ: کرمی، سبز‌آبی، مرجانی</Badge>
            <Badge tone="neutral">سایز: A5، A4</Badge>
          </div>
          <Button type="button" variant="outline" icon="add" className="self-start px-lg">
            افزودن ویژگی
          </Button>
        </FormSection>

        <FormSection title="سئو" icon="travel_explore">
          <Input name="metaTitle" label="عنوان متا" />
          <Textarea name="metaDescription" label="توضیحات متا" rows={2} />
          <Input name="slug" label="نشانی صفحه" className="latin" placeholder="daily-planner-a5" />
        </FormSection>
      </FormLayout>
    </form>
  );
}
