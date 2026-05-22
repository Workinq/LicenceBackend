import type { Editor } from '@tiptap/react';
import {
  Bold, Italic, Underline as UnderlineIcon, Strikethrough,
  Heading2, Heading3, Heading4,
  List, ListOrdered, ListChecks,
  Code, SquareCode, Quote, Minus,
  AlignLeft, AlignCenter, AlignRight,
  Link as LinkIcon, Image as ImageIcon,
  Baseline, Highlighter, Table as TableIcon,
} from 'lucide-react';
import {
  DropdownMenu, DropdownMenuTrigger, DropdownMenuContent,
} from '@/components/ui/dropdown-menu';
import { cn } from '@/lib/utils';
import { PAGE_TEXT_COLORS, PAGE_HIGHLIGHT_COLORS } from './tiptap-extensions';

export function ProductPageToolbar({
  editor,
  onImageClick,
}: {
  editor: Editor;
  onImageClick: () => void;
}) {
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
    <div className="flex flex-wrap items-center gap-0.5 rounded-md border border-border p-1">
      <ToolbarButton active={editor.isActive('heading', { level: 2 })} label="Heading 2"
        onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}>
        <Heading2 className="size-4" />
      </ToolbarButton>
      <ToolbarButton active={editor.isActive('heading', { level: 3 })} label="Heading 3"
        onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()}>
        <Heading3 className="size-4" />
      </ToolbarButton>
      <ToolbarButton active={editor.isActive('heading', { level: 4 })} label="Heading 4"
        onClick={() => editor.chain().focus().toggleHeading({ level: 4 }).run()}>
        <Heading4 className="size-4" />
      </ToolbarButton>

      <ToolbarDivider />

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
      <ToolbarButton active={editor.isActive('strike')} label="Strikethrough"
        onClick={() => editor.chain().focus().toggleStrike().run()}>
        <Strikethrough className="size-4" />
      </ToolbarButton>
      <ToolbarButton active={editor.isActive('code')} label="Inline code"
        onClick={() => editor.chain().focus().toggleCode().run()}>
        <Code className="size-4" />
      </ToolbarButton>

      <ToolbarDivider />

      <SwatchDropdown
        label="Text colour"
        icon={<Baseline className="size-4" />}
        colors={PAGE_TEXT_COLORS}
        onPick={(value) => editor.chain().focus().setColor(value).run()}
        onClear={() => editor.chain().focus().unsetColor().run()}
      />
      <SwatchDropdown
        label="Highlight"
        icon={<Highlighter className="size-4" />}
        colors={PAGE_HIGHLIGHT_COLORS}
        onPick={(value) => editor.chain().focus().toggleHighlight({ color: value }).run()}
        onClear={() => editor.chain().focus().unsetHighlight().run()}
      />

      <ToolbarDivider />

      <ToolbarButton active={editor.isActive('bulletList')} label="Bullet list"
        onClick={() => editor.chain().focus().toggleBulletList().run()}>
        <List className="size-4" />
      </ToolbarButton>
      <ToolbarButton active={editor.isActive('orderedList')} label="Numbered list"
        onClick={() => editor.chain().focus().toggleOrderedList().run()}>
        <ListOrdered className="size-4" />
      </ToolbarButton>
      <ToolbarButton active={editor.isActive('taskList')} label="Task list"
        onClick={() => editor.chain().focus().toggleTaskList().run()}>
        <ListChecks className="size-4" />
      </ToolbarButton>

      <ToolbarDivider />

      <ToolbarButton active={editor.isActive('blockquote')} label="Quote"
        onClick={() => editor.chain().focus().toggleBlockquote().run()}>
        <Quote className="size-4" />
      </ToolbarButton>
      <ToolbarButton active={editor.isActive('codeBlock')} label="Code block"
        onClick={() => editor.chain().focus().toggleCodeBlock().run()}>
        <SquareCode className="size-4" />
      </ToolbarButton>
      <ToolbarButton active={false} label="Divider"
        onClick={() => editor.chain().focus().setHorizontalRule().run()}>
        <Minus className="size-4" />
      </ToolbarButton>

      <ToolbarDivider />

      <ToolbarButton active={editor.isActive({ textAlign: 'left' })} label="Align left"
        onClick={() => editor.chain().focus().setTextAlign('left').run()}>
        <AlignLeft className="size-4" />
      </ToolbarButton>
      <ToolbarButton active={editor.isActive({ textAlign: 'center' })} label="Align center"
        onClick={() => editor.chain().focus().setTextAlign('center').run()}>
        <AlignCenter className="size-4" />
      </ToolbarButton>
      <ToolbarButton active={editor.isActive({ textAlign: 'right' })} label="Align right"
        onClick={() => editor.chain().focus().setTextAlign('right').run()}>
        <AlignRight className="size-4" />
      </ToolbarButton>

      <ToolbarDivider />

      <ToolbarButton active={editor.isActive('link')} label="Link" onClick={setLink}>
        <LinkIcon className="size-4" />
      </ToolbarButton>
      <ToolbarButton active={false} label="Image" onClick={onImageClick}>
        <ImageIcon className="size-4" />
      </ToolbarButton>
      <TableDropdown editor={editor} />
    </div>
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

function ToolbarDivider() {
  return <span className="mx-0.5 h-5 w-px bg-border" aria-hidden="true" />;
}

function SwatchDropdown({
  label,
  icon,
  colors,
  onPick,
  onClear,
}: {
  label: string;
  icon: React.ReactNode;
  colors: { name: string; value: string }[];
  onPick: (value: string) => void;
  onClear: () => void;
}) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label={label}
          title={label}
          className="inline-flex size-8 items-center justify-center rounded text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink"
        >
          {icon}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent className="p-2">
        <div className="grid grid-cols-3 gap-1">
          {colors.map((c) => (
            <button
              key={c.value}
              type="button"
              aria-label={c.name}
              title={c.name}
              onClick={() => { onPick(c.value); }}
              className="size-7 rounded border border-border"
              style={{ backgroundColor: c.value }}
            />
          ))}
        </div>
        <button
          type="button"
          onClick={() => { onClear(); }}
          className="mt-2 w-full rounded px-2 py-1 text-xs text-ink-muted hover:bg-surface-sunken hover:text-ink"
        >
          Clear
        </button>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function TableDropdown({ editor }: { editor: Editor }) {
  const items: { label: string; run: () => void }[] = [
    { label: 'Insert table', run: () => editor.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run() },
    { label: 'Add row above', run: () => editor.chain().focus().addRowBefore().run() },
    { label: 'Add row below', run: () => editor.chain().focus().addRowAfter().run() },
    { label: 'Add column before', run: () => editor.chain().focus().addColumnBefore().run() },
    { label: 'Add column after', run: () => editor.chain().focus().addColumnAfter().run() },
    { label: 'Delete row', run: () => editor.chain().focus().deleteRow().run() },
    { label: 'Delete column', run: () => editor.chain().focus().deleteColumn().run() },
    { label: 'Delete table', run: () => editor.chain().focus().deleteTable().run() },
  ];

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label="Table"
          title="Table"
          className="inline-flex size-8 items-center justify-center rounded text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink"
        >
          <TableIcon className="size-4" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent className="min-w-44">
        {items.map((item) => (
          <button
            key={item.label}
            type="button"
            onClick={() => { item.run(); }}
            className="block w-full rounded px-2 py-1.5 text-left text-sm text-ink hover:bg-surface-sunken"
          >
            {item.label}
          </button>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
