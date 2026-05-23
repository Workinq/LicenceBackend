import { useId } from 'react';
import { Upload } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';
import { cn } from '@/lib/utils';

interface ImagePickerButtonProps {
  onSelect: (file: File) => void;
  label?: string;
  disabled?: boolean;
}

export function ImagePickerButton({ onSelect, label = 'Choose image', disabled = false }: Readonly<ImagePickerButtonProps>) {
  const inputId = useId();
  return (
    <>
      <label
        htmlFor={inputId}
        className={cn(
          buttonVariants({ variant: 'outline', size: 'sm' }),
          'cursor-pointer',
          disabled && 'pointer-events-none opacity-50',
        )}
      >
        <Upload className="size-4" aria-hidden="true" />
        <span className="ml-1.5">{label}</span>
      </label>
      <input
        id={inputId}
        type="file"
        accept="image/png,image/jpeg,image/webp"
        className="sr-only"
        disabled={disabled}
        onChange={(e) => {
          const file = e.target.files?.[0];
          if (file) onSelect(file);
          e.target.value = '';
        }}
      />
    </>
  );
}
