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

describe('ProductPageToolbar extra coverage', () => {
  it('clicking Heading 2 runs toggleHeading with level 2', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Heading 2' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleHeading', args: [{ level: 2 }] },
    ]);
  });

  it('clicking Heading 4 runs toggleHeading with level 4', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Heading 4' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleHeading', args: [{ level: 4 }] },
    ]);
  });

  it('clicking the inline formatting buttons runs the matching toggle commands', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);

    await userEvent.click(screen.getByRole('button', { name: 'Italic' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleItalic', args: [] },
    ]);

    await userEvent.click(screen.getByRole('button', { name: 'Underline' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleUnderline', args: [] },
    ]);

    await userEvent.click(screen.getByRole('button', { name: 'Strikethrough' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleStrike', args: [] },
    ]);

    await userEvent.click(screen.getByRole('button', { name: 'Inline code' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleCode', args: [] },
    ]);
  });

  it('clicking the list buttons runs the matching list toggles', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);

    await userEvent.click(screen.getByRole('button', { name: 'Bullet list' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleBulletList', args: [] },
    ]);

    await userEvent.click(screen.getByRole('button', { name: 'Numbered list' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleOrderedList', args: [] },
    ]);

    await userEvent.click(screen.getByRole('button', { name: 'Task list' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleTaskList', args: [] },
    ]);
  });

  it('clicking Quote, Code block, and Divider runs the matching block commands', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);

    await userEvent.click(screen.getByRole('button', { name: 'Quote' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleBlockquote', args: [] },
    ]);

    await userEvent.click(screen.getByRole('button', { name: 'Code block' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleCodeBlock', args: [] },
    ]);

    await userEvent.click(screen.getByRole('button', { name: 'Divider' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'setHorizontalRule', args: [] },
    ]);
  });

  it('clicking Align left and Align right calls setTextAlign with the matching argument', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);

    await userEvent.click(screen.getByRole('button', { name: 'Align left' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'setTextAlign', args: ['left'] },
    ]);

    await userEvent.click(screen.getByRole('button', { name: 'Align right' }));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'setTextAlign', args: ['right'] },
    ]);
  });

  it('seeds the link prompt with the current href attribute', async () => {
    const fake = makeFakeEditor();
    fake.attributes.link = { href: 'https://existing.example' };
    const promptSpy = vi.spyOn(window, 'prompt').mockReturnValue('https://new.example');
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Link' }));
    expect(promptSpy).toHaveBeenCalledWith('Link URL', 'https://existing.example');
  });

  it('picking a highlight swatch toggles highlight with the chosen value', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Highlight' }));
    const yellow = await screen.findByRole('button', { name: 'Yellow' });
    await userEvent.click(yellow);
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'toggleHighlight', args: [{ color: '#fff3b0' }] },
    ]);
  });

  it('the text colour Clear button unsets the colour', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Text colour' }));
    const clears = await screen.findAllByRole('button', { name: /^clear$/i });
    await userEvent.click(clears[0]);
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'unsetColor', args: [] },
    ]);
  });

  it('the highlight Clear button unsets the highlight', async () => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Highlight' }));
    const clears = await screen.findAllByRole('button', { name: /^clear$/i });
    await userEvent.click(clears[0]);
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method: 'unsetHighlight', args: [] },
    ]);
  });

  it.each([
    ['Add row above', 'addRowBefore'],
    ['Add row below', 'addRowAfter'],
    ['Add column before', 'addColumnBefore'],
    ['Add column after', 'addColumnAfter'],
    ['Delete row', 'deleteRow'],
    ['Delete column', 'deleteColumn'],
    ['Delete table', 'deleteTable'],
  ])('table dropdown item %s runs the matching chain command', async (label, method) => {
    const fake = makeFakeEditor();
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    await userEvent.click(screen.getByRole('button', { name: 'Table' }));
    await userEvent.click(await screen.findByText(label));
    expect(fake.lastChain().calls).toEqual([
      { method: 'focus', args: [] },
      { method, args: [] },
    ]);
  });

  it('marks alignment buttons as pressed via the object isActive predicate', () => {
    const fake = makeFakeEditor();
    fake.active.add(`__obj:${JSON.stringify({ textAlign: 'center' })}`);
    render(<ProductPageToolbar editor={fake.editor} onImageClick={() => {}} />);
    expect(screen.getByRole('button', { name: 'Align center' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Align left' })).toHaveAttribute('aria-pressed', 'false');
  });
});
