'use client';

import { useRouter } from 'next/navigation';
import { useRef, useState, type ChangeEvent, type FormEvent } from 'react';
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
  variantPriced = false,
}: {
  product?: AdminProductDto;
  brands: CatalogueOptionDto[];
  categories: CatalogueOptionDto[];
  collections: CatalogueOptionDto[];
  /**
   * Whether the product has active combinations that carry their own price.
   *
   * When it does, the base price below is not what anybody pays: a shopper who
   * picks a size is charged that size's price, so demanding a figure here was
   * asking the operator to invent one. False on the create form, where there
   * are no combinations yet and a product with no price would be a free one.
   */
  variantPriced?: boolean;
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
  /** Which «ویرایش تخصصی» tile is mid-save, so only that one shows a spinner. */
  const [opening, setOpening] = useState<string | null>(null);
  // `persist` is called from a tile as well as from submit, and a tile click
  // is not a submit — there is no event to read the form off.
  const formRef = useRef<HTMLFormElement>(null);

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

  /**
   * Validates the form and writes it, answering with the product's id.
   *
   * `null` means nothing was written and the reason is on screen — a field
   * error, or the server's own sentence. Split out of the submit handler
   * because «ثبت محصول» is no longer the only thing that saves: a tile in
   * «ویرایش تخصصی» saves too, since those screens are addressed by an id that
   * does not exist until something does.
   *
   * Reads the form through a ref rather than an event target, for the same
   * reason — a tile click is not a submit and has no form to read off the
   * event.
   */
  async function persist(): Promise<string | null> {
    const form = formRef.current;
    if (!form) return null;

    const data = new FormData(form);
    const next: Errors = {};

    const title = String(data.get('title') ?? '').trim();
    const sku = String(data.get('sku') ?? '').trim();
    const rawPrice = normalizeDigitsInput(String(data.get('price') ?? '')).trim();
    const price = Number(rawPrice);
    const stock = Number(normalizeDigitsInput(String(data.get('stock') ?? '')));

    if (title.length < 3) next.title = 'نام محصول را کامل وارد کنید.';
    // Underscore included alongside the hyphen: the seeder's own SKUs
    // (`BZ-P_01`) use it, and the API places no charset restriction on this
    // field beyond a length cap — "English, at least 4 characters" is a rule
    // this form invented on its own. Without underscore here, opening any
    // seeded product and saving it — without even touching the SKU field —
    // failed this check on the value the product already had.
    if (!/^[A-Za-z0-9_-]{4,}$/.test(sku)) next.sku = 'کد کالا باید انگلیسی و حداقل ۴ نویسه باشد.';
    // Required only while nothing else can price the product. Once its
    // combinations carry prices, this is a display figure — and a negative or
    // unreadable one is still wrong, which is what the second clause catches.
    if (!variantPriced && (!Number.isFinite(price) || price <= 0)) {
      next.price = 'قیمت را وارد کنید.';
    } else if (Number.isFinite(price) && price < 0) {
      next.price = 'قیمت نمی‌تواند منفی باشد.';
    }
    if (!Number.isFinite(stock) || stock < 0) next.stock = 'موجودی نمی‌تواند منفی باشد.';
    // The API refuses an empty set too — a product filed nowhere is one that
    // shows up in no listing at all. Caught here so the operator is told which
    // field to fix instead of being handed the server's word for it.
    if (pickedCategories.length === 0) next.categories = 'دست‌کم یک دسته‌بندی انتخاب کنید.';

    setErrors(next);
    if (Object.keys(next).length > 0) {
      /*
        The first bad field, brought into view. «اطلاعات پایه» is a long way up
        the page from a tile in «ویرایش تخصصی», and an operator who clicks
        «تنوع محصول» and is returned nothing they can see would read that as
        the tile being broken rather than as the form being incomplete.

        After a frame, because the message being scrolled to is rendered by the
        `setErrors` above and is not in the document yet. The selector matches
        the error line every field grows — `FieldShell` gives it an id — as well
        as the invalid control itself, because the picker marks the line but not
        its trigger.
      */
      requestAnimationFrame(() => {
        form
          .querySelector('[aria-invalid="true"], [id$="-error"]')
          ?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      });
      return null;
    }

    setSaved(false);
    try {
      const written = await postJson<{ id?: string }>('/api/admin/products', {
        ...(product ? { id: product.id } : null),
        title,
        sku,
        // Null rather than the 0 an empty box parses to. The API leaves a
        // field it was not sent alone, and on a variant-priced product this box
        // is allowed to be empty — posting 0 would put «۰ تومان» on the
        // product card instead of leaving the listing price as it was.
        price: rawPrice.length > 0 ? price : null,
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

      // The id of what was just written: the API answers `{ id }` on a create,
      // and on an edit the product already had one. Mock mode answers neither,
      // which is why the fallback is the existing product rather than a throw.
      return typeof written?.id === 'string' && written.id.length > 0
        ? written.id
        : (product?.id ?? null);
    } catch (cause) {
      setErrors({ form: cause instanceof Error ? cause.message : 'ذخیره محصول انجام نشد.' });
      return null;
    }
  }

  /**
   * «ثبت محصول» / «ذخیره تغییرات».
   *
   * A create hands over to the product's own page. Two things were wrong
   * without that. The specialised screens are addressed by id, so an operator
   * who had just added a product could not reach any of them. And pressing
   * save a second time posted a body with no id, which the API reads as
   * another new product: what the operator got was a duplicate under a
   * suffixed slug rather than the save they asked for.
   *
   * `replace` rather than `push`, so «انصراف» and the browser's back button go
   * where the operator came from instead of to an empty create form for a
   * product they have already made.
   */
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    setSaving(true);
    // Kept out of a `finally`, so the button stays busy through the navigation
    // instead of flicking back to «ثبت محصول» while the next page loads.
    const id = await persist();

    if (id && !product) {
      router.replace(`/products/${id}`);
      return;
    }

    if (id) setSaved(true);
    setSaving(false);
  }

  /**
   * A tile in «ویرایش تخصصی», on a product that does not exist yet.
   *
   * Saves first and opens the screen the operator asked for, rather than
   * making them press «ثبت محصول», wait to land somewhere else, and find the
   * tile again. Entering the basic information and going straight on to
   * variants or volume tiers is the order the work actually happens in, and
   * the id those screens need is the only reason it could not be.
   */
  async function saveAndOpen(slug: string) {
    setOpening(slug);
    const id = await persist();

    if (id) {
      router.replace(`/products/${id}/${slug}`);
      return;
    }

    setOpening(null);
  }

  return (
    <form ref={formRef} onSubmit={submit} noValidate>
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

        {/*
          Gone entirely once combinations price the product, rather than left on
          screen as an optional box.

          A product that sells by size has no single price to type: size 1 is
          ۱۰٬۰۰۰ and size 2 is ۱۱٬۰۰۰, and the shop charges whichever the
          shopper picks. A box here would be a figure that looks authoritative,
          overrides nothing, and disagrees with every size — so the section is
          replaced by the one sentence that says where the prices actually live.

          The product's own price is still stored, because the listing card has
          to print something; the API keeps it equal to the cheapest combination
          rather than to whatever was typed before the sizes existed. See
          `SaveSkusAsync`.
        */}
        {variantPriced ? (
          <FormSection title="قیمت‌گذاری" icon="payments">
            <p className="gap-sm text-body-md text-on-surface-variant flex items-start leading-relaxed">
              <Icon name="tune" size={20} className="mt-2xs shrink-0 text-primary" />
              <span>
                قیمت این محصول از روی تنوع‌هایش تعیین می‌شود و مشتری قیمت همان ترکیبی را می‌پردازد
                که انتخاب می‌کند. برای تغییر قیمت‌ها و تخفیف هر سایز یا رنگ، به «ویرایش تخصصی ←
                تنوع محصول» بروید.
              </span>
            </p>
          </FormSection>
        ) : (
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
        )}

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

          Live on the create form too, not dimmed. Every one of these screens is
          addressed by the product's id, which does not exist until something
          saves — so on a new product the tile saves first and then opens the
          screen. That is the order the work actually happens in: fill in the
          basics, then go and set up the variants. Making the operator press
          «ثبت محصول», land somewhere else and find the tile again was three
          steps to do what they had already asked for in one.
        */}
        <FormSection
          title="ویرایش تخصصی"
          icon="widgets"
          description={
            isEdit && product
              ? 'هر کدام از این‌ها صفحه‌ی خودش را دارد.'
              : 'اطلاعات پایه را پر کنید و مستقیم وارد هرکدام شوید؛ محصول همان لحظه ثبت می‌شود.'
          }
        >
          <ProductTools
            productId={product?.id}
            onOpen={saveAndOpen}
            opening={opening}
            busy={saving || opening !== null}
          />
        </FormSection>

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
