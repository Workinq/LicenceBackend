import { useState } from 'react';
import { Check, Copy } from 'lucide-react';
import { cn } from '@/lib/utils';

interface KeyChipProps {
  value: string;
  display?: string;
  className?: string;
}

export function KeyChip({ value, display, className }: KeyChipProps) {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      // clipboard unavailable
    }
  };

  return (
    <button
      type="button"
      onClick={() => { void copy(); }}
      className={cn(
        'inline-flex items-center gap-1 rounded-[3px] border border-border bg-surface-sunken px-1 font-mono text-[11.5px] leading-[1.5] text-foreground transition-colors hover:bg-muted',
        className,
      )}
      aria-label={`Copy ${value}`}
    >
      <span>{display ?? value}</span>
      {copied ? <Check className="size-3 text-status-active-fg" aria-hidden /> : <Copy className="size-3 text-ink-subtle" aria-hidden />}
    </button>
  );
}
