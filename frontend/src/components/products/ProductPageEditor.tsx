import { useId, useReducer, useRef } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { EditorContent, useEditor, type JSONContent } from '@tiptap/react';
import { toast } from 'sonner';
import {
  Bold, Italic, Underline as UnderlineIcon, Heading2, Heading3,
  List, ListOrdered, Link as LinkIcon, Image as ImageIcon,
} from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { productPageExtensions } from './tiptap-extensions';
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
}: {
  productId: string;
  initialContent: JSONContent | null;
}) {
  const queryClient = useQueryClient();
  const fileInputId = useId();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [, forceTick] = useReducer((n: number) => n + 1, 0);

  const editor = useEditor({
    extensions: productPageExtensions(),
    content: initialContent ?? '',
    onTransaction: () => { forceTick(); },
    editorProps: {
      attributes: {
        class: 'tiptap-content min-h-48 rounded-md border border-input bg-surface-elevated px-3 py-2',
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
    },
    onError: (error) => {
      toast.error(errorDetail(error, 'Could not upload the image.'));
    },
  });

  if (!editor) return null;

  const setLink = () => {
    const previous = (editor.getAttributes('link').href as string | undefined) ?? '';
    const url = window.prompt('Link URL', previous);
    if (url === null) return;
    if (url === '') {
      editor.chain().focus().extendMarkRange('link').unsetLink().run();
      return;
    }
    editor.chain().focus().extendMarkRange('link').setLink({ href: url }).run();
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Product page</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <p className="text-sm text-ink-muted">
          Compose the rich page customers see on the product detail page.
        </p>

        <div className="flex flex-wrap gap-1 rounded-md border border-border p-1">
          <ToolbarButton active={editor.isActive('heading', { level: 2 })} label="Heading 2"
            onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}>
            <Heading2 className="size-4" />
          </ToolbarButton>
          <ToolbarButton active={editor.isActive('heading', { level: 3 })} label="Heading 3"
            onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()}>
            <Heading3 className="size-4" />
          </ToolbarButton>
          <ToolbarButton active={editor.isActive('bold')} label="Bold"
            onClick={() => editor.chain().focus().toggleBold().run()}>
            <Bold className="size-4" />
          </ToolbarButton>
          <ToolbarButton active={editor.isActive('italic')} label="Italic"
            onClick={() => editor.chain().focus().toggleItalic().run()}>
            <Italic className="size-4" />
          </ToolbarButton>
          <ToolbarButton active={editor.isActive('underline')} label="Underline"
            onClick={() => editor.chain().focus().toggleUnderline().run()}>
            <UnderlineIcon className="size-4" />
          </ToolbarButton>
          <ToolbarButton active={editor.isActive('bulletList')} label="Bullet list"
            onClick={() => editor.chain().focus().toggleBulletList().run()}>
            <List className="size-4" />
          </ToolbarButton>
          <ToolbarButton active={editor.isActive('orderedList')} label="Numbered list"
            onClick={() => editor.chain().focus().toggleOrderedList().run()}>
            <ListOrdered className="size-4" />
          </ToolbarButton>
          <ToolbarButton active={editor.isActive('link')} label="Link" onClick={setLink}>
            <LinkIcon className="size-4" />
          </ToolbarButton>
          <ToolbarButton active={false} label="Image"
            onClick={() => fileInputRef.current?.click()}>
            <ImageIcon className="size-4" />
          </ToolbarButton>
        </div>

        <EditorContent editor={editor} />

        <input
          id={fileInputId}
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

        <Button type="button" onClick={() => { saveMutation.mutate(); }} disabled={saveMutation.isPending}>
          Save product page
        </Button>
      </CardContent>
    </Card>
  );
}

function ToolbarButton({
  active,
  label,
  onClick,
  children,
}: {
  active: boolean;
  label: string;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      aria-pressed={active}
      title={label}
      onClick={onClick}
      className={cn(
        'inline-flex size-8 items-center justify-center rounded transition-colors',
        active ? 'bg-ink text-surface-elevated' : 'text-ink-muted hover:text-ink hover:bg-surface-sunken',
      )}
    >
      {children}
    </button>
  );
}
