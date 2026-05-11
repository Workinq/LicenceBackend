import { useState } from 'react';
import { Check, Copy, TriangleAlert } from 'lucide-react';
import { Button } from '@/components/ui/button';

export function SecretRevealOnce({ label, value }: { label: string; value: string }) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      // clipboard unavailable - nothing useful to do
    }
  };

  return (
    <div className="rounded-lg border border-accent bg-accent-soft/40 p-4">
      <div className="flex items-start gap-2 text-sm text-ink">
        <TriangleAlert className="mt-0.5 size-4 shrink-0 text-accent" aria-hidden="true" />
        <p>
          <span className="font-medium">{label}.</span> Copy this now - you will not be able to see it again.
        </p>
      </div>
      <div className="mt-3 flex items-center gap-2">
        <code className="flex-1 overflow-x-auto rounded bg-surface-elevated px-2 py-1.5 font-mono text-sm text-ink">
          {value}
        </code>
        <Button type="button" variant="outline" size="sm" aria-label="Copy" onClick={() => { void copy(); }}>
          {copied ? <Check className="size-3.5" aria-hidden="true" /> : <Copy className="size-3.5" aria-hidden="true" />}
          <span className="ml-1.5">{copied ? 'Copied' : 'Copy'}</span>
        </Button>
      </div>
    </div>
  );
}
