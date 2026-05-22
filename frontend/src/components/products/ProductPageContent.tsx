import { EditorContent, useEditor, type JSONContent } from '@tiptap/react';
import { productPageExtensions } from './tiptap-extensions';

export function ProductPageContent({ content }: { content: JSONContent }) {
  const editor = useEditor({
    editable: false,
    extensions: productPageExtensions(),
    content,
  });

  if (!editor) return null;
  return <EditorContent editor={editor} className="tiptap-content" />;
}
