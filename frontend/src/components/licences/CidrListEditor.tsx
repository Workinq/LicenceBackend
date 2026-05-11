import { Plus, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

interface Props {
  cidrs: string[];
  onChange: (next: string[]) => void;
}

export function CidrListEditor({ cidrs, onChange }: Props) {
  return (
    <div className="space-y-2">
      {cidrs.map((c, i) => (
        <div key={i} className="flex items-center gap-2">
          <Input
            value={c}
            placeholder="CIDR e.g. 203.0.113.0/24"
            onChange={(e) => { onChange(cidrs.map((r, j) => (j === i ? e.target.value : r))); }}
            className="flex-1"
          />
          <Button
            type="button"
            variant="ghost"
            size="icon"
            aria-label="Remove CIDR"
            onClick={() => { onChange(cidrs.filter((_, j) => j !== i)); }}
          >
            <X className="size-4" aria-hidden="true" />
          </Button>
        </div>
      ))}
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={() => { onChange([...cidrs, '']); }}
      >
        <Plus className="size-4" aria-hidden="true" />
        <span className="ml-1.5">Add CIDR</span>
      </Button>
    </div>
  );
}
