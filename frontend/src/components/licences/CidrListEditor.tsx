import { useRef } from 'react';
import { Plus, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

interface Props {
  cidrs: string[];
  onChange: (next: string[]) => void;
}

export function CidrListEditor({ cidrs, onChange }: Readonly<Props>) {
  const idsRef = useRef<string[]>([]);
  while (idsRef.current.length < cidrs.length) {
    idsRef.current.push(crypto.randomUUID());
  }
  if (idsRef.current.length > cidrs.length) {
    idsRef.current = idsRef.current.slice(0, cidrs.length);
  }

  const handleRemove = (index: number) => {
    idsRef.current = idsRef.current.filter((_, j) => j !== index);
    onChange(cidrs.filter((_, j) => j !== index));
  };

  const handleAdd = () => {
    idsRef.current = [...idsRef.current, crypto.randomUUID()];
    onChange([...cidrs, '']);
  };

  return (
    <div className="space-y-2">
      {cidrs.map((c, i) => (
        <div key={idsRef.current[i]} className="flex items-center gap-2">
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
            onClick={() => { handleRemove(i); }}
          >
            <X className="size-4" aria-hidden="true" />
          </Button>
        </div>
      ))}
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={handleAdd}
      >
        <Plus className="size-4" aria-hidden="true" />
        <span className="ml-1.5">Add CIDR</span>
      </Button>
    </div>
  );
}
