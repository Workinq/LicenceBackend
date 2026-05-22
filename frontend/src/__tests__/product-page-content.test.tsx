import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ProductPageContent } from '@/components/products/ProductPageContent';

describe('ProductPageContent', () => {
  it('renders text from a ProseMirror document', async () => {
    const doc = {
      type: 'doc',
      content: [
        { type: 'heading', attrs: { level: 2 }, content: [{ type: 'text', text: 'Overview' }] },
        { type: 'paragraph', content: [{ type: 'text', text: 'A great product.' }] },
      ],
    };

    render(<ProductPageContent content={doc} />);

    expect(await screen.findByRole('heading', { level: 2, name: 'Overview' })).toBeInTheDocument();
    expect(await screen.findByText('A great product.')).toBeInTheDocument();
  });

  it('renders a code block, a table, and a coloured span', async () => {
    const doc = {
      type: 'doc',
      content: [
        {
          type: 'codeBlock',
          attrs: { language: 'javascript' },
          content: [{ type: 'text', text: 'const x = 1;' }],
        },
        {
          type: 'paragraph',
          content: [
            {
              type: 'text',
              marks: [{ type: 'textStyle', attrs: { color: '#b3261e' } }],
              text: 'Red text',
            },
          ],
        },
        {
          type: 'table',
          content: [
            {
              type: 'tableRow',
              content: [
                { type: 'tableHeader', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'Cell head' }] }] },
              ],
            },
            {
              type: 'tableRow',
              content: [
                { type: 'tableCell', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'Cell body' }] }] },
              ],
            },
          ],
        },
      ],
    };

    const { container } = render(<ProductPageContent content={doc} />);

    // CodeBlockLowlight runs syntax highlighting, which splits the code text
    // across multiple token <span>s, so findByText('const x = 1;') would not
    // match. Assert on the <code> element's normalized textContent instead.
    const code = await screen.findByText((_content, element) => {
      return element?.tagName === 'CODE' && element.textContent === 'const x = 1;';
    });
    expect(code).toBeInTheDocument();
    expect(code.closest('pre')).toBeInTheDocument();

    const red = await screen.findByText('Red text');
    expect(red).toBeInTheDocument();
    expect(red).toHaveStyle({ color: '#b3261e' });

    expect(await screen.findByText('Cell head')).toBeInTheDocument();
    expect(await screen.findByText('Cell body')).toBeInTheDocument();
    expect(container.querySelector('table')).toBeInTheDocument();
  });
});
