import { useState } from 'react';
import { Check, Copy } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

export function LicenceKey({ value, className }: Readonly<{ value: string; className?: string }>) {
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
    <span className={cn('inline-flex items-center gap-1.5', className)}>
      <code className="rounded bg-surface-sunken px-1.5 py-0.5 font-mono text-sm text-ink">{value}</code>
      <Button
        type="button"
        variant="ghost"
        size="icon"
        className="size-7"
        aria-label="Copy"
        onClick={() => { void copy(); }}
      >
        {copied ? <Check className="size-3.5" aria-hidden="true" /> : <Copy className="size-3.5" aria-hidden="true" />}
      </Button>
    </span>
  );
}
