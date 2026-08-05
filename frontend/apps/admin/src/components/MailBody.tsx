'use client';

import { useEffect, useRef, useState } from 'react';
import { Icon } from '@bojan/ui';

/**
 * Renders one message body.
 *
 * The HTML has already been allow-listed on the server. This is the second
 * layer, and it exists because the first one is a library that could have a
 * bypass: the body goes into an iframe with **neither** `allow-scripts` nor
 * `allow-same-origin`, so anything that survived sanitizing lands in an opaque
 * origin that cannot execute, cannot read the panel's DOM, and cannot touch its
 * cookies.
 *
 * `srcDoc` rather than a URL: there is no document to fetch, and a blob URL
 * would put the content on an origin related to this one.
 */
export function MailBody({
  html,
  text,
  hadRemoteContent,
}: {
  html: string;
  text: string;
  hadRemoteContent: boolean;
}) {
  const frame = useRef<HTMLIFrameElement>(null);
  const [height, setHeight] = useState(120);

  // The frame has no fixed height that would be right: an email is any length.
  // Measured after it loads and re-measured on resize, because a sandboxed
  // frame cannot tell its parent anything itself.
  useEffect(() => {
    if (!html) return undefined;

    function measure() {
      const document_ = frame.current?.contentDocument;
      if (!document_) return;
      setHeight(Math.min(Math.max(document_.body.scrollHeight + 32, 120), 4000));
    }

    const timer = window.setTimeout(measure, 60);
    window.addEventListener('resize', measure);

    return () => {
      window.clearTimeout(timer);
      window.removeEventListener('resize', measure);
    };
  }, [html]);

  if (!html) {
    // No HTML part, or a body that could not be parsed — the plain-text
    // alternative is always safe because it is rendered as text.
    return (
      <p className="whitespace-pre-wrap text-body-md leading-loose text-on-surface">
        {text || '(بدون متن)'}
      </p>
    );
  }

  return (
    <div className="flex flex-col gap-sm">
      {hadRemoteContent && (
        <p className="flex items-center gap-xs rounded-lg bg-surface-container-low px-md py-sm text-caption text-on-surface-variant">
          <Icon name="info" size={16} />
          تصاویر بیرونی این پیام برای حفظ حریم خصوصی نمایش داده نشدند.
        </p>
      )}

      <iframe
        ref={frame}
        title="متن پیام"
        // No allow-scripts and no allow-same-origin. Adding either one — even
        // together, which browsers warn about — would defeat the whole point of
        // this element.
        sandbox=""
        referrerPolicy="no-referrer"
        onLoad={() => {
          const document_ = frame.current?.contentDocument;
          if (document_) {
            setHeight(Math.min(Math.max(document_.body.scrollHeight + 32, 120), 4000));
          }
        }}
        srcDoc={`<!doctype html><html dir="rtl"><head><meta charset="utf-8">
<style>
  body{margin:0;padding:8px;font-family:Tahoma,system-ui,sans-serif;font-size:14px;
       line-height:1.9;color:#1a1c1e;word-break:break-word}
  img{max-width:100%}
  table{max-width:100%}
  a{color:#00696e}
</style></head><body>${html}</body></html>`}
        style={{ height }}
        className="w-full rounded-lg border border-outline-variant bg-white"
      />
    </div>
  );
}
