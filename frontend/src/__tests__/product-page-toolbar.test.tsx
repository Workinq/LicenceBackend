import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Editor } from '@tiptap/react';
import { ProductPageToolbar } from '../components/products/ProductPageToolbar';

interface ChainRecorder {
  calls: { method: string; args: unknown[] }[];
  ran: boolean;
}

function makeChain(recorder: ChainRecorder): Record<string, (...args: unknown[]) => unknown> {
  const proxy: Record<string, (...args: unknown[]) => unknown> = {};
  const handler = new Proxy(proxy, {
    get(_target, prop: string) {
      return (...args: unknown[]) => {
        if (prop === 'run') {
          recorder.ran = true;
          return true;
        }
        recorder.calls.push({ method: prop, args });
        return handler;
      };
    },
  });
  return handler;
}

interface FakeEditor {
  editor: Editor;
  chains: ChainRecorder[];
  active: Set<string>;
  attributes: Record<string, Record<string, unknown>>;
  lastChain: () => ChainRecorder;
}

function makeFakeEditor(): FakeEditor {
  const chains: ChainRecorder[] = [];
  const active = new Set<string>();
  const attributes: Record<string, Record<string, unknown>> = {};
  const editor = {
    chain: () => {
      const recorder: ChainRecorder = { calls: [], ran: false };
      chains.push(recorder);
      return makeChain(recorder);
    },
    isActive: (name: string | Record<string, unknown>, attrs?: Record<string, unknown>) => {
      if (typeof name === 'string') {
        if (!attrs) return active.has(name);
        const key = `${name}:${JSON.stringify(attrs)}`;
        return active.has(key);
      }
      const key = `__obj:${JSON.stringify(name)}`;
      return active.has(key);
    },
    getAttributes: (name: string) => attributes[name] ?? {},
  } as unknown as Editor;
  return { editor, chains, active, attributes, lastChain: () => chains[chains.length - 1] };
}

beforeEach(() => {
  vi.restoreAllMocks();
});

describe('ProductPageToolbar', () => {
  it('renders all primary toolbar buttons', () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    for (const label of [
      'Heading 2', 'Heading 3', 'Heading 4',
      'Bold', 'Italic', 'Underline', 'Strikethrough', 'Inline code',
      'Text colour', 'Highlight',
      'Bullet list', 'Numbered list', 'Task list',
      'Quote', 'Code block', 'Divider',
      'Align left', 'Align center', 'Align right',
      'Link', 'Image', 'Table',
    ]) {
      expect(screen.getByRole('button', { name: label })).toBeInTheDocument();
    }
  });

  it('marks the Bold button as pressed when bold is active', () => {
    const fake = makeFakeEditor();
    fake.active.add('bold');
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    expect(screen.getByRole('button', { name: 'Bold' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Italic' })).toHaveAttribute('aria-pressed', 'false');
  });

  it('clicking Bold runs the toggleBold chain', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Bold' }));
    const chain = fake.lastChain();
    expect(chain.calls.map((c) => c.method)).toEqual(['focus', 'toggleBold']);
    expect(chain.ran).toBe(true);
  });

  it('clicking Heading 3 runs toggleHeading with level 3', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Heading 3' }));
    const chain = fake.lastChain();
    expect(chain.calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleHeading', args: [{ level: 3 }] },
    ]);
    expect(chain.ran).toBe(true);
  });

  it('clicking Align center calls setTextAlign with center', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Align center' }));
    const chain = fake.lastChain();
    expect(chain.calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'setTextAlign', args: ['center'] },
    ]);
  });

  it('clicking the Image button invokes the onImageClick callback', async () => {
    const fake = makeFakeEditor();
    const onImageClick = vi.fn();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={onImageClick} />);
    await userEvent.click(screen.getByRole('button', { name: 'Image' }));
    expect(onImageClick).toHaveBeenCalledTimes(1);
  });

  it('clicking Link with a non-empty URL calls setLink with the href', async () => {
    const fake = makeFakeEditor();
    const promptSpy = vi.spyOn(window, 'prompt').mockReturnValue('https://example.com');
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Link' }));
    expect(promptSpy).toHaveBeenCalled();
    const chain = fake.lastChain();
    expect(chain.calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'extendMarkRange', args: ['link'] },
      { method: 'setLink', args: [{ href: 'https://example.com' }] },
    ]);
  });

  it('clicking Link with an empty URL unsets the link', async () => {
    const fake = makeFakeEditor();
    vi.spyOn(window, 'prompt').mockReturnValue('');
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Link' }));
    const chain = fake.lastChain();
    expect(chain.calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'extendMarkRange', args: ['link'] },
      { method: 'unsetLink', args: [] },
    ]);
  });

  it('cancelling the link prompt does nothing', async () => {
    const fake = makeFakeEditor();
    vi.spyOn(window, 'prompt').mockReturnValue(null);
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Link' }));
    expect(fake.chains).toHaveLength(0);
  });

  it('picking a text colour swatch calls setColor with the chosen hex', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Text colour' }));
    const red = await screen.findByRole('menuitem', { name: 'Red' }).catch(() => null);
    const swatch = red ?? (await screen.findByRole('button', { name: 'Red' }));
    await userEvent.click(swatch);
    const chain = fake.lastChain();
    expect(chain.calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'setColor', args: ['#b3261e'] },
    ]);
  });

  it('clicking Insert table from the table dropdown calls insertTable', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Table' }));
    const insert = await screen.findByText('Insert table');
    await userEvent.click(insert);
    const chain = fake.lastChain();
    expect(chain.calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'insertTable', args: [{ rows: 3, cols: 3, withHeaderRow: true }] },
    ]);
  });
});
