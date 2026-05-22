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
});
