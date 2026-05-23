import { useState } from 'react';
import { Check, ChevronDown } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover';
import { cn } from '@/lib/utils';

export interface FilterOption<V extends string = string> {
  value: V;
  label: string;
}

interface FilterChipProps<V extends string> {
  label: string;
  value: V;
  options: FilterOption<V>[];
  onChange: (next: V) => void;
  className?: string;
}

export function FilterChip<V extends string>({
  label,
  value,
  options,
  onChange,
  className,
}: FilterChipProps<V>) {
  const [open, setOpen] = useState(false);
  const current = options.find((o) => o.value === value);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        className={cn(
          'inline-flex h-7 items-center gap-1.5 rounded-[4px] border border-border bg-card px-2.5 text-[12px] font-medium text-foreground transition-colors',
          'hover:bg-surface-sunken focus:outline-none focus:ring-2 focus:ring-ring/40',
          className,
        )}
      >
        <span className="text-ink-muted">{label}:</span>
        <span>{current?.label ?? value}</span>
        <ChevronDown className="size-3 text-ink-subtle" aria-hidden />
      </PopoverTrigger>
      <PopoverContent align="start" className="w-44 p-1">
        <ul role="listbox" className="flex flex-col">
          {options.map((opt) => {
            const selected = opt.value === value;
            return (
              <li key={opt.value}>
                <button
                  type="button"
                  role="option"
                  aria-selected={selected}
                  onClick={() => {
                    onChange(opt.value);
                    setOpen(false);
                  }}
                  className={cn(
                    'flex w-full items-center justify-between rounded-sm px-2 py-1.5 text-left text-[12.5px] text-foreground transition-colors',
                    'hover:bg-surface-sunken',
                  )}
                >
                  <span>{opt.label}</span>
                  {selected && <Check className="size-3.5 text-accent" aria-hidden />}
                </button>
              </li>
            );
          })}
        </ul>
      </PopoverContent>
    </Popover>
  );
}
