import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeAll } from 'vitest';

beforeAll(() => {
  // The formatters rely on the Persian calendar and Persian locale data.
  // If the runtime shipped a trimmed ICU, date assertions would silently drift,
  // so fail loudly here instead of producing confusing diffs later.
  const persian = new Intl.DateTimeFormat('fa-IR-u-ca-persian', { year: 'numeric' });
  if (persian.resolvedOptions().calendar !== 'persian') {
    throw new Error(
      'Node was built without full ICU: the Persian calendar is unavailable, so date tests cannot run.',
    );
  }
});

afterEach(() => {
  cleanup();
});
