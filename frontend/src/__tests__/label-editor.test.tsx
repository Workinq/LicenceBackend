import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LicenceLabelEditor } from '../components/licences/LicenceLabelEditor';

vi.mock('../api/me-licences', () => ({
  updateMyLicenceLabel: vi.fn(),
}));
import { updateMyLicenceLabel } from '../api/me-licences';

function renderEditor(props: { licenceId?: string; label?: string | null; editable?: boolean } = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <LicenceLabelEditor
        licenceId={props.licenceId ?? 'lic-1'}
        label={props.label ?? null}
        editable={props.editable ?? true}
      />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(updateMyLicenceLabel).mockReset();
});

describe('LicenceLabelEditor', () => {
  it('renders a static label and no edit button when not editable', () => {
    renderEditor({ label: 'My laptop', editable: false });
    expect(screen.getByText('My laptop')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /edit label/i })).not.toBeInTheDocument();
  });

  it('renders a dash placeholder when there is no label', () => {
    renderEditor({ label: null, editable: false });
    expect(screen.getByText('-')).toBeInTheDocument();
  });

  it('shows the edit button and enters edit mode when clicked', async () => {
    renderEditor({ label: 'Old', editable: true });
    await userEvent.click(screen.getByRole('button', { name: /edit label/i }));
    expect(screen.getByDisplayValue('Old')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /save/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
  });

  it('saves a trimmed non-empty label via updateMyLicenceLabel', async () => {
    vi.mocked(updateMyLicenceLabel).mockResolvedValue({} as never);
    renderEditor({ licenceId: 'lic-9', label: '', editable: true });
    await userEvent.click(screen.getByRole('button', { name: /edit label/i }));
    const input = screen.getByPlaceholderText(/no label/i);
    await userEvent.type(input, '  desk  ');
    await userEvent.click(screen.getByRole('button', { name: /save/i }));
    await waitFor(() => {
      expect(vi.mocked(updateMyLicenceLabel)).toHaveBeenCalledWith('lic-9', { label: 'desk' });
    });
  });

  it('sends label null when the draft is empty after trimming', async () => {
    vi.mocked(updateMyLicenceLabel).mockResolvedValue({} as never);
    renderEditor({ licenceId: 'lic-2', label: 'something', editable: true });
    await userEvent.click(screen.getByRole('button', { name: /edit label/i }));
    const input = screen.getByDisplayValue('something');
    await userEvent.clear(input);
    await userEvent.type(input, '   ');
    await userEvent.click(screen.getByRole('button', { name: /save/i }));
    await waitFor(() => {
      expect(vi.mocked(updateMyLicenceLabel)).toHaveBeenCalledWith('lic-2', { label: null });
    });
  });

  it('submits on Enter and exits edit mode on success', async () => {
    vi.mocked(updateMyLicenceLabel).mockResolvedValue({} as never);
    renderEditor({ label: 'a', editable: true });
    await userEvent.click(screen.getByRole('button', { name: /edit label/i }));
    const input = screen.getByDisplayValue('a');
    await userEvent.clear(input);
    await userEvent.type(input, 'b{Enter}');
    await waitFor(() => {
      expect(vi.mocked(updateMyLicenceLabel)).toHaveBeenCalledWith('lic-1', { label: 'b' });
    });
    await waitFor(() => {
      expect(screen.queryByRole('button', { name: /save/i })).not.toBeInTheDocument();
    });
  });

  it('exits edit mode on Escape without calling the API', async () => {
    renderEditor({ label: 'keep', editable: true });
    await userEvent.click(screen.getByRole('button', { name: /edit label/i }));
    const input = screen.getByDisplayValue('keep');
    await userEvent.type(input, 'X{Escape}');
    expect(screen.queryByRole('button', { name: /save/i })).not.toBeInTheDocument();
    expect(vi.mocked(updateMyLicenceLabel)).not.toHaveBeenCalled();
  });

  it('shows an error message when the mutation fails', async () => {
    vi.mocked(updateMyLicenceLabel).mockRejectedValue(new Error('Label too long'));
    renderEditor({ label: '', editable: true });
    await userEvent.click(screen.getByRole('button', { name: /edit label/i }));
    const input = screen.getByPlaceholderText(/no label/i);
    await userEvent.type(input, 'newlabel');
    await userEvent.click(screen.getByRole('button', { name: /save/i }));
    expect(await screen.findByText(/label too long/i)).toBeInTheDocument();
  });

  it('cancels edit mode without saving via the Cancel button', async () => {
    renderEditor({ label: 'original', editable: true });
    await userEvent.click(screen.getByRole('button', { name: /edit label/i }));
    await userEvent.click(screen.getByRole('button', { name: /cancel/i }));
    expect(screen.queryByRole('button', { name: /save/i })).not.toBeInTheDocument();
    expect(vi.mocked(updateMyLicenceLabel)).not.toHaveBeenCalled();
  });
});
