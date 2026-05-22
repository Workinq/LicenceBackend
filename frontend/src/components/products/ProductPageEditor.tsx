import { useReducer, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { EditorContent, useEditor, type JSONContent } from '@tiptap/react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { productPageExtensions } from './tiptap-extensions';
import { ProductPageToolbar } from './ProductPageToolbar';
import { updateProduct } from '@/api/products';
import { uploadProductContentImage } from '@/api/product-content-images';
import { ApiError } from '@/auth/api-client';

function errorDetail(error: unknown, fallback: string): string {
  return error instanceof ApiError && error.body && typeof error.body === 'object' && 'detail' in error.body
    ? String((error.body as Record<string, unknown>).detail)
    : fallback;
}

export function ProductPageEditor({
  productId,
  initialContent,
  onDirtyChange,
}: {
  productId: string;
  initialContent: JSONContent | null;
  onDirtyChange?: (dirty: boolean) => void;
}) {
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const dirtyRef = useRef(false);
  const [, forceTick] = useReducer((n: number) => n + 1, 0);

  const markDirty = () => {
    if (!dirtyRef.current) {
      dirtyRef.current = true;
      onDirtyChange?.(true);
    }
  };

  const editor = useEditor({
    extensions: productPageExtensions(),
    content: initialContent ?? '',
    onTransaction: () => { forceTick(); },
    onUpdate: () => { markDirty(); },
    editorProps: {
      attributes: {
        class: 'tiptap-content min-h-96 rounded-md border border-input bg-surface-elevated px-4 py-3',
      },
    },
  });

  const saveMutation = useMutation({
    mutationFn: () => updateProduct(productId, {
      displayName: null,
      description: null,
      tagline: null,
      isPublic: null,
      price: null,
      currency: null,
      sortOrder: null,
      pageContent: editor?.getJSON() ?? null,
    }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['products', 'detail', productId] });
      dirtyRef.current = false;
      onDirtyChange?.(false);
      toast.success('Product page saved.');
    },
    onError: (error) => {
      toast.error(errorDetail(error, 'Could not save the product page.'));
    },
  });

  const uploadMutation = useMutation({
    mutationFn: (file: File) => uploadProductContentImage(productId, file),
    onSuccess: (image) => {
      editor?.chain().focus().setImage({ src: `/api${image.url}` }).run();
      markDirty();
    },
    onError: (error) => {
      toast.error(errorDetail(error, 'Could not upload the image.'));
    },
  });

  if (!editor) return null;

  return (
    <div className="space-y-3">
      <div className="sticky top-0 z-10 flex flex-wrap items-center justify-between gap-2 bg-surface py-2">
        <ProductPageToolbar editor={editor} onImageClick={() => fileInputRef.current?.click()} />
        <Button type="button" onClick={() => { saveMutation.mutate(); }} disabled={saveMutation.isPending}>
          Save product page
        </Button>
      </div>

      <EditorContent editor={editor} />

      <input
        ref={fileInputRef}
        type="file"
        accept="image/png,image/jpeg,image/webp"
        className="sr-only"
        disabled={uploadMutation.isPending}
        onChange={(e) => {
          const f = e.target.files?.[0];
          if (f) uploadMutation.mutate(f);
          e.target.value = '';
        }}
      />
    </div>
  );
}
