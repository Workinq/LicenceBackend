import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import type { Extensions } from '@tiptap/react';

/**
 * The deliberately small extension set shared by the admin editor and the
 * read-only customer renderer, so both produce identical output.
 */
export function productPageExtensions(): Extensions {
  return [
    StarterKit.configure({
      heading: { levels: [2, 3] },
      link: {
        openOnClick: false,
        protocols: ['http', 'https', 'mailto'],
        HTMLAttributes: { rel: 'noopener noreferrer nofollow', target: '_blank' },
      },
    }),
    Image,
  ];
}
