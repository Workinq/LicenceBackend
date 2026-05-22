import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import { TextStyle, Color } from '@tiptap/extension-text-style';
import { Highlight } from '@tiptap/extension-highlight';
import { TextAlign } from '@tiptap/extension-text-align';
import { CodeBlockLowlight } from '@tiptap/extension-code-block-lowlight';
import { TableKit } from '@tiptap/extension-table';
import { TaskList, TaskItem } from '@tiptap/extension-list';
import { createLowlight } from 'lowlight';
import javascript from 'highlight.js/lib/languages/javascript';
import typescript from 'highlight.js/lib/languages/typescript';
import json from 'highlight.js/lib/languages/json';
import bash from 'highlight.js/lib/languages/bash';
import css from 'highlight.js/lib/languages/css';
import xml from 'highlight.js/lib/languages/xml';
import python from 'highlight.js/lib/languages/python';
import csharp from 'highlight.js/lib/languages/csharp';
import sql from 'highlight.js/lib/languages/sql';
import type { Extensions } from '@tiptap/react';

const lowlight = createLowlight();
lowlight.register({ javascript, typescript, json, bash, css, xml, python, csharp, sql });

export interface PageColor {
  name: string;
  value: string;
}

/** Fixed curated text colours offered by the editor (stored as hex in the document). */
export const PAGE_TEXT_COLORS: PageColor[] = [
  { name: 'Red', value: '#b3261e' },
  { name: 'Orange', value: '#b3591e' },
  { name: 'Green', value: '#2d6a2d' },
  { name: 'Blue', value: '#1e5fb3' },
  { name: 'Purple', value: '#6a3da8' },
  { name: 'Grey', value: '#5d4d3e' },
];

/** Fixed curated highlight colours offered by the editor. */
export const PAGE_HIGHLIGHT_COLORS: PageColor[] = [
  { name: 'Yellow', value: '#fff3b0' },
  { name: 'Green', value: '#c9e7c9' },
  { name: 'Blue', value: '#cfe0f5' },
  { name: 'Pink', value: '#f5cfe0' },
  { name: 'Orange', value: '#f5ddc0' },
];

/**
 * The extension set shared by the admin editor and the read-only customer
 * renderer, so both produce identical output.
 */
export function productPageExtensions(): Extensions {
  return [
    StarterKit.configure({
      heading: { levels: [2, 3, 4] },
      codeBlock: false,
      link: {
        openOnClick: false,
        protocols: ['http', 'https', 'mailto'],
        HTMLAttributes: { rel: 'noopener noreferrer nofollow', target: '_blank' },
      },
    }),
    Image,
    TextStyle,
    Color.configure({ types: ['textStyle'] }),
    Highlight.configure({ multicolor: true }),
    TextAlign.configure({ types: ['heading', 'paragraph'] }),
    CodeBlockLowlight.configure({ lowlight }),
    TableKit.configure({ resizable: true }),
    TaskList,
    TaskItem.configure({ nested: true }),
  ];
}
