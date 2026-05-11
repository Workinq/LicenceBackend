import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CurrencyCombobox } from '../components/CurrencyCombobox';

vi.mock('../api/currencies', async () => {
  const actual = await vi.importActual<typeof import('../api/currencies')>('../api/currencies');
  return { ...actual, fetchCurrencyList: vi.fn() };
});
import { fetchCurrencyList } from '../api/currencies';

function renderCombobox(props: { value: string; onChange: (v: string) => void }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <CurrencyCombobox {...props} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(fetchCurrencyList).mockReset();
});

describe('CurrencyCombobox', () => {
  it('lists three-letter codes from the fetched list and excludes non-3-letter entries', async () => {
    vi.mocked(fetchCurrencyList).mockResolvedValue({ usd: 'United States Dollar', eur: 'Euro', '1inch': '1inch Network', usdt: 'Tether' });
    const onChange = vi.fn();
    renderCombobox({ value: '', onChange });
    await userEvent.click(screen.getByRole('combobox'));
    await screen.findByPlaceholderText(/search currenc/i);
    expect(screen.getByText('EUR - Euro')).toBeInTheDocument();
    expect(screen.getByText('USD - United States Dollar')).toBeInTheDocument();
    expect(screen.queryByText(/1inch/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/usdt/i)).not.toBeInTheDocument();
  });

  it('calls onChange with the uppercase code when an option is picked', async () => {
    vi.mocked(fetchCurrencyList).mockResolvedValue({ usd: 'United States Dollar', eur: 'Euro' });
    const onChange = vi.fn();
    renderCombobox({ value: '', onChange });
    await userEvent.click(screen.getByRole('combobox'));
    await userEvent.type(await screen.findByPlaceholderText(/search currenc/i), 'eur');
    await userEvent.click(await screen.findByText('EUR - Euro'));
    expect(onChange).toHaveBeenCalledWith('EUR');
  });

  it('shows the current value on the trigger even before the list loads', () => {
    vi.mocked(fetchCurrencyList).mockReturnValue(new Promise(() => {}));
    renderCombobox({ value: 'ZWL', onChange: vi.fn() });
    expect(screen.getByRole('combobox', { name: /ZWL/i })).toBeInTheDocument();
  });

  it('falls back to a built-in list when the fetch fails', async () => {
    vi.mocked(fetchCurrencyList).mockRejectedValue(new Error('cdn down'));
    renderCombobox({ value: '', onChange: vi.fn() });
    await userEvent.click(screen.getByRole('combobox'));
    expect(await screen.findByText('USD - United States Dollar')).toBeInTheDocument();
  });
});
