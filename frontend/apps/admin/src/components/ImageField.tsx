'use client';

import { useRef, useState } from 'react';
import { Button, Icon, Input } from '@bojan/ui';

/**
 * Picking an image for an entity that has one — a category icon, a brand logo,
 * a collection or article cover.
 *
 * These were text boxes labelled «نشانی تصویر», and the address they asked for
 * could not exist. `AdminCatalogueService.IsStorableImage` accepts only a URL
 * this API's own upload endpoint issued — same base, the right folder, and a
 * generated 32-character name — and refuses everything else with
 * `invalid-request: cover`. So a URL typed by hand was rejected by definition,
 * pasting one from anywhere else was rejected, and there was no control
 * anywhere on these screens that could produce a value the field would take.
 * The cover on an article simply could not be set.
 *
 * The refusal is right: a shop that stored links to images on somebody else's
 * server would show broken pictures the day that server changed its mind. What
 * was missing was the other half — the upload that mints an address the rule
 * accepts. The file goes to `/api/upload/{folder}`, which forwards to the API,
 * and the URL it returns is what this submits.
 */
export function ImageField({
  name,
  label,
  folder,
  defaultValue = '',
  hint,
}: {
  name: string;
  label: string;
  /** One of the API's upload folders — products, brands, collections, content, campaigns. */
  folder: string;
  defaultValue?: string;
  hint?: string;
}) {
  const [url, setUrl] = useState(defaultValue);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const picker = useRef<HTMLInputElement>(null);

  async function upload(file: File | undefined) {
    if (!file) return;

    setUploading(true);
    setError(null);
    try {
      const body = new FormData();
      body.append('file', file);

      const response = await fetch(`/api/upload/${folder}`, { method: 'POST', body });
      const payload = (await response.json().catch(() => null)) as
        | { url?: string; error?: string }
        | null;

      if (!response.ok || !payload?.url) {
        throw new Error(payload?.error ?? 'بارگذاری تصویر انجام نشد.');
      }

      setUrl(payload.url);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'بارگذاری تصویر انجام نشد.');
    } finally {
      setUploading(false);
      // Cleared so choosing the same file again still fires a change event —
      // which is what an operator does after a failed upload.
      if (picker.current) picker.current.value = '';
    }
  }

  return (
    <div className="flex flex-col gap-sm">
      {/*
        The value the form actually posts. Read-only rather than hidden: the
        operator can see what is stored and copy it, and cannot type an address
        the server would refuse.
      */}
      <Input
        name={name}
        label={label}
        value={url}
        readOnly
        className="latin"
        dir="ltr"
        placeholder="تصویری انتخاب نشده است"
        {...(hint ? { hint } : null)}
        {...(error ? { error } : null)}
      />

      {url && (
        // A plain img, not next/image: the address is only known at runtime and
        // points at whatever host serves this installation's uploads.
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={url}
          alt=""
          className="h-32 w-full max-w-xs rounded-lg border border-outline-variant object-cover"
        />
      )}

      <div className="flex flex-wrap items-center gap-sm">
        <input
          ref={picker}
          type="file"
          accept="image/jpeg,image/png,image/webp,image/gif"
          className="hidden"
          onChange={(event) => upload(event.target.files?.[0])}
        />
        <Button
          type="button"
          variant="outline"
          size="sm"
          icon="upload"
          loading={uploading}
          onClick={() => picker.current?.click()}
        >
          {url ? 'انتخاب تصویر دیگر' : 'بارگذاری تصویر'}
        </Button>

        {url && (
          <button
            type="button"
            onClick={() => setUrl('')}
            className="flex items-center gap-2xs rounded p-xs text-caption text-on-surface-variant transition-colors hover:text-error"
          >
            <Icon name="delete" size={18} />
            حذف تصویر
          </button>
        )}
      </div>
    </div>
  );
}
