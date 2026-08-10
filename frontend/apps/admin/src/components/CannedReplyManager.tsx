'use client';

import { useState, type FormEvent } from 'react';
import { Button, Card, FormStatus, Icon } from '@bojan/ui';
import { FormSection } from '@/components/FormLayout';
import { postJson } from '@/lib/submit';

export interface CannedReply {
  id: string;
  title: string;
  body: string;
}

/**
 * Screen 132 — the canned replies support uses.
 *
 * The whole screen was inert: the add button, both row controls and the form
 * at the bottom had no handlers. Editing happens in place, because the design
 * gives the row a pencil but no second screen to send the operator to.
 */
export function CannedReplyManager({ initial }: { initial: CannedReply[] }) {
  const [replies, setReplies] = useState(initial);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [draft, setDraft] = useState({ title: '', body: '' });
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const field =
    'w-full rounded-lg border border-outline-variant bg-surface-container-lowest px-md text-body-md text-on-surface placeholder:text-outline focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/30';

  function startEdit(reply: CannedReply) {
    setEditingId(reply.id);
    setDraft({ title: reply.title, body: reply.body });
    setError(null);
  }

  function cancelEdit() {
    setEditingId(null);
    setDraft({ title: '', body: '' });
    setError(null);
  }

  async function save(event: FormEvent) {
    event.preventDefault();

    const title = draft.title.trim();
    const body = draft.body.trim();
    if (!title || !body) {
      setError('عنوان و متن پاسخ را کامل وارد کنید.');
      return;
    }

    setPending(true);
    setError(null);
    try {
      await postJson('/api/admin/canned-replies', {
        ...(editingId ? { id: editingId } : null),
        title,
        body,
      });

      setReplies((current) =>
        editingId
          ? current.map((reply) => (reply.id === editingId ? { ...reply, title, body } : reply))
          : [...current, { id: `cr-${Date.now()}`, title, body }],
      );
      cancelEdit();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'ذخیره پاسخ انجام نشد.');
    } finally {
      setPending(false);
    }
  }

  async function remove(reply: CannedReply) {
    const previous = replies;
    setReplies((current) => current.filter((item) => item.id !== reply.id));
    if (editingId === reply.id) cancelEdit();

    try {
      await postJson('/api/admin/canned-replies', { id: reply.id, deleted: true });
    } catch (cause) {
      // Put it back rather than pretend it is gone.
      setReplies(previous);
      setError(cause instanceof Error ? cause.message : 'حذف پاسخ انجام نشد.');
    }
  }

  return (
    <>
      <div className="flex flex-col gap-md">
        {replies.map((reply) => (
          <Card key={reply.id} className="flex flex-col gap-sm p-lg">
            <div className="flex flex-wrap items-start justify-between gap-sm">
              <h3 className="text-label-md font-semibold text-primary">{reply.title}</h3>

              <div className="flex shrink-0 items-center gap-xs">
                <button
                  type="button"
                  aria-label={`ویرایش ${reply.title}`}
                  onClick={() => startEdit(reply)}
                  className="rounded p-xs text-on-surface-variant transition-colors hover:bg-surface-container hover:text-primary"
                >
                  <Icon name="edit" size={18} />
                </button>
                <button
                  type="button"
                  aria-label={`حذف ${reply.title}`}
                  onClick={() => remove(reply)}
                  className="rounded p-xs text-on-surface-variant transition-colors hover:bg-error-container hover:text-error"
                >
                  <Icon name="delete" size={18} />
                </button>
              </div>
            </div>

            <p className="text-body-md leading-loose text-on-surface-variant">{reply.body}</p>
          </Card>
        ))}
      </div>

      <FormSection
        title={editingId ? 'ویرایش پاسخ آماده' : 'افزودن پاسخ آماده'}
        icon={editingId ? 'edit_note' : 'add_comment'}
      >
        <form onSubmit={save} className="flex flex-col gap-md">
          <input
            value={draft.title}
            onChange={(event) => setDraft((current) => ({ ...current, title: event.target.value }))}
            aria-label="عنوان پاسخ"
            placeholder="عنوان پاسخ"
            maxLength={120}
            className={`h-12 ${field}`}
          />
          <textarea
            rows={3}
            value={draft.body}
            onChange={(event) => setDraft((current) => ({ ...current, body: event.target.value }))}
            aria-label="متن پاسخ"
            placeholder="متن پاسخ"
            maxLength={2000}
            className={`py-md ${field}`}
          />

          <FormStatus error={error} />

          <div className="flex flex-wrap gap-sm">
            <Button type="submit" loading={pending} className="px-xl">
              {editingId ? 'ذخیره تغییرات' : 'ذخیره پاسخ'}
            </Button>

            {editingId && (
              <Button type="button" variant="ghost" onClick={cancelEdit} className="px-xl">
                انصراف
              </Button>
            )}
          </div>
        </form>
      </FormSection>
    </>
  );
}
