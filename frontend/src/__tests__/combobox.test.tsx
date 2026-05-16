import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Combobox } from '../components/Combobox';

const opts = [
  { value: 'a', label: 'Alpha' },
  { value: 'b', label: 'Bravo' },
];

describe('Combobox', () => {
  it('shows the placeholder when nothing is selected', () => {
    render(<Combobox options={opts} value="" onChange={() => {}} placeholder="Pick one" />);
    expect(screen.getByRole('combobox', { name: /pick one/i })).toBeInTheDocument();
  });

  it('shows the selected option label on the trigger', () => {
    render(<Combobox options={opts} value="b" onChange={() => {}} placeholder="Pick one" />);
    expect(screen.getByRole('combobox', { name: /bravo/i })).toBeInTheDocument();
  });

  it('opens, filters by typed text, and calls onChange with the option value when picked', async () => {
    const onChange = vi.fn();
    render(<Combobox options={opts} value="" onChange={onChange} placeholder="Pick one" searchPlaceholder="Search" />);
    await userEvent.click(screen.getByRole('combobox'));
    const input = await screen.findByPlaceholderText('Search');
    await userEvent.type(input, 'brav');
    await userEvent.click(await screen.findByText('Bravo'));
    expect(onChange).toHaveBeenCalledWith('b');
  });

  it('shows the empty text when nothing matches the search', async () => {
    render(<Combobox options={[{ value: 'a', label: 'Alpha' }]} value="" onChange={() => {}} searchPlaceholder="Search" emptyText="Nothing found" />);
    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.type(await screen.findByPlaceholderText('Search'), 'zzz');
    expect(await screen.findByText('Nothing found')).toBeInTheDocument();
  });

  it('renders the footer action and fires onSelect when clicked', async () => {
    const onSelect = vi.fn();
    render(
      <Combobox
        options={opts}
        value=""
        onChange={() => {}}
        searchPlaceholder="Search"
        footerAction={{ label: 'Create new thing', onSelect }}
      />,
    );
    await userEvent.click(screen.getByRole('combobox'));
    const footer = await screen.findByRole('button', { name: /create new thing/i });
    await userEvent.click(footer);
    expect(onSelect).toHaveBeenCalledTimes(1);
  });

  it('keeps the footer action visible when the search filters all results out', async () => {
    const onSelect = vi.fn();
    render(
      <Combobox
        options={opts}
        value=""
        onChange={() => {}}
        searchPlaceholder="Search"
        emptyText="Nothing found"
        footerAction={{ label: 'Create new thing', onSelect }}
      />,
    );
    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.type(await screen.findByPlaceholderText('Search'), 'zzz');
    expect(await screen.findByText('Nothing found')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create new thing/i })).toBeInTheDocument();
  });
});
