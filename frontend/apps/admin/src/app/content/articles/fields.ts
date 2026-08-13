import type { EntityField } from '@/components/EntityForm';
import type { AdminArticleDto } from '@/lib/api/types';

/**
 * The article editor's fields, shared by the create and edit screens.
 *
 * One definition rather than two copies: the two screens drifted before — the
 * create form set the content `kind` and the edit form did not — and a field
 * list is exactly the kind of thing that stops matching without anybody
 * noticing until a save writes the wrong shape.
 */
export function articleSections(article?: AdminArticleDto): { title: string; icon: string; fields: EntityField[] }[] {
  return [
    {
      title: 'محتوای مقاله',
      icon: 'article',
      fields: [
        { name: 'title', label: 'عنوان مقاله', required: true, value: article?.title ?? '' },
        {
          name: 'slug',
          label: 'نشانی (slug)',
          latin: true,
          value: article?.slug ?? '',
          hint: 'اگر خالی بماند از روی عنوان ساخته می‌شود. آدرس مقاله در سایت همین است.',
        },
        {
          name: 'excerpt',
          label: 'خلاصه',
          kind: 'textarea',
          value: article?.excerpt ?? '',
          hint: 'روی کارت مقاله در فهرست مجله نمایش داده می‌شود.',
        },
        {
          name: 'body',
          label: 'متن مقاله',
          kind: 'textarea',
          value: article?.body ?? '',
          // The two rules the API's block parser applies, said where they are
          // typed rather than left to be discovered.
          hint: 'یک خط خالی بین پاراگراف‌ها. خطی که با ## شروع شود، تیتر می‌شود. زمان خواندن خودکار حساب می‌شود.',
        },
      ],
    },
  ];
}

export function articleAsideSections(
  article?: AdminArticleDto,
): { title: string; icon: string; fields: EntityField[] }[] {
  return [
    {
      title: 'انتشار',
      icon: 'visibility',
      fields: [
        {
          name: 'status',
          label: 'وضعیت',
          kind: 'select',
          value: article?.status ?? 'draft',
          options: [
            { value: 'draft', label: 'پیش‌نویس' },
            { value: 'published', label: 'منتشر شده' },
          ],
          hint: 'فقط «منتشر شده» در مجله‌ی سایت دیده می‌شود.',
        },
        {
          name: 'category',
          label: 'دسته‌ی مقاله',
          value: article?.category ?? '',
          hint: 'روی کارت مقاله برچسب می‌خورد — مثلاً «راهنمای خرید».',
        },
        {
          name: 'featured',
          label: 'مقاله ویژه',
          kind: 'switch',
          checked: article?.featured ?? false,
          hint: 'بالای صفحه‌ی مجله، بزرگ‌تر از بقیه نمایش داده می‌شود.',
        },
      ],
    },
    {
      title: 'تصویر کاور',
      icon: 'image',
      fields: [
        {
          name: 'cover',
          label: 'تصویر کاور',
          kind: 'image',
          folder: 'content',
          value: article?.cover ?? '',
          hint: 'تصویر بارگذاری می‌شود؛ نشانی از جای دیگر پذیرفته نمی‌شود.',
        },
      ],
    },
  ];
}
