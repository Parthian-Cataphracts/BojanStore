import type { Metadata } from 'next';
import { OfflineNotice } from '@/components/status/OfflineNotice';

export const metadata: Metadata = {
  title: 'عدم اتصال اینترنت',
  robots: { index: false },
};

/** Screen 40 — No internet connection. */
export default function OfflinePage() {
  return <OfflineNotice />;
}
