import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RegenerateKeyDialog } from '../components/licences/RegenerateKeyDialog';

vi.mock('../api/licences', () => ({
  regenerateLicenceKey: vi.fn(),
}));
import { regenerateLicenceKey } from '../api/licences';

vi.mock('sonner', () => ({
  toast: { error: vi.fn(), success: vi.fn() },
}));
import { toast } from 'sonner';

function renderDialog(props: { licenceId?: string; hasKey?: boolean; regenerate?: (id: string) => Promise<unknown> } = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <RegenerateKeyDialog
        licenceId={props.licenceId ?? 'lic-1'}
        hasKey={props.hasKey ?? true}
        regenerate={props.regenerate as never}
      />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(regenerateLicenceKey).mockReset();
  vi.mocked(toast.error).mockReset();
});

describe('RegenerateKeyDialog', () => {
  it('renders a Regenerate key trigger when hasKey is true', () => {
    renderDialog({ hasKey: true });
    expect(screen.getByRole('button', { name: /regenerate key/i })).toBeInTheDocument();
  });

  it('renders a Generate key trigger when hasKey is false', () => {
    renderDialog({ hasKey: false });
    expect(screen.getByRole('button', { name: /^generate key$/i })).toBeInTheDocument();
  });

  it('confirming the destructive prompt calls regenerateLicenceKey with reason null', async () => {
    vi.mocked(regenerateLicenceKey).mockResolvedValue({ licenceKey: 'LK-new-123' } as never);
    renderDialog({ licenceId: 'lic-7', hasKey: true });
    await userEvent.click(screen.getByRole('button', { name: /regenerate key/i }));
    const confirms = await screen.findAllByRole('button', { name: /regenerate key/i });
    await userEvent.click(confirms[confirms.length - 1]);
    await waitFor(() => {
      expect(vi.mocked(regenerateLicenceKey)).toHaveBeenCalledWith('lic-7', { reason: null });
    });
  });

  it('shows the new licence key in a reveal-once dialog after success', async () => {
    vi.mocked(regenerateLicenceKey).mockResolvedValue({ licenceKey: 'LK-new-123' } as never);
    renderDialog({ hasKey: true });
    await userEvent.click(screen.getByRole('button', { name: /regenerate key/i }));
    const confirms = await screen.findAllByRole('button', { name: /regenerate key/i });
    await userEvent.click(confirms[confirms.length - 1]);
    expect(await screen.findByText('LK-new-123')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /done/i })).toBeInTheDocument();
  });

  it('uses the injected regenerate function when provided', async () => {
    const custom = vi.fn().mockResolvedValue({ licenceKey: 'LK-custom-1' });
    renderDialog({ licenceId: 'lic-3', hasKey: false, regenerate: custom });
    await userEvent.click(screen.getByRole('button', { name: /^generate key$/i }));
    const confirms = await screen.findAllByRole('button', { name: /generate key/i });
    await userEvent.click(confirms[confirms.length - 1]);
    await waitFor(() => {
      expect(custom).toHaveBeenCalledWith('lic-3');
    });
    expect(vi.mocked(regenerateLicenceKey)).not.toHaveBeenCalled();
    expect(await screen.findByText('LK-custom-1')).toBeInTheDocument();
  });

  it('closes the reveal dialog when Done is clicked', async () => {
    vi.mocked(regenerateLicenceKey).mockResolvedValue({ licenceKey: 'LK-x' } as never);
    renderDialog({ hasKey: true });
    await userEvent.click(screen.getByRole('button', { name: /regenerate key/i }));
    const confirms = await screen.findAllByRole('button', { name: /regenerate key/i });
    await userEvent.click(confirms[confirms.length - 1]);
    expect(await screen.findByText('LK-x')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /done/i }));
    await waitFor(() => {
      expect(screen.queryByText('LK-x')).not.toBeInTheDocument();
    });
  });

  it('shows a toast error when the mutation fails', async () => {
    vi.mocked(regenerateLicenceKey).mockRejectedValue(new Error('boom'));
    renderDialog({ hasKey: true });
    await userEvent.click(screen.getByRole('button', { name: /regenerate key/i }));
    const confirms = await screen.findAllByRole('button', { name: /regenerate key/i });
    await userEvent.click(confirms[confirms.length - 1]);
    await waitFor(() => {
      expect(vi.mocked(toast.error)).toHaveBeenCalledWith('Could not regenerate the licence key.');
    });
  });
});
