import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LicenceBindings } from '../components/licences/LicenceBindings';

vi.mock('../api/licences', () => ({
  updateLicenceHwid: vi.fn(),
  updateLicenceIpAllowlist: vi.fn(),
}));
import { updateLicenceIpAllowlist } from '../api/licences';

function makeLicence(over: Record<string, unknown> = {}) {
  return {
    id: 'lic-1',
    productId: 'p',
    productSlug: 's',
    userId: 'u',
    userEmail: 'a@b.com',
    status: 'active',
    expiresAt: null,
    notes: null,
    hwidBound: false,
    ipAllowlist: null,
    createdAt: '2026-01-01T00:00:00Z',
    ...over,
  };
}

function renderBindings(over: Record<string, unknown> = {}) {
  const queryClient = new QueryClient();
  render(
    <QueryClientProvider client={queryClient}>
      <LicenceBindings licence={makeLicence(over)} />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.mocked(updateLicenceIpAllowlist).mockReset();
});

describe('LicenceBindings', () => {
  it('shows the bound state and a Clear HWID action when the licence is HWID bound', () => {
    renderBindings({ hwidBound: true });
    expect(screen.getByText(/bound/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /clear hwid/i })).toBeInTheDocument();
  });

  it('shows the not-bound state and no Clear action when the licence is not HWID bound', () => {
    renderBindings({ hwidBound: false });
    expect(screen.getByText(/not bound/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /clear hwid/i })).not.toBeInTheDocument();
  });

  it('renders the existing CIDR allowlist rows', () => {
    renderBindings({ ipAllowlist: ['10.0.0.0/8', '192.168.1.0/24'] });
    expect(screen.getByDisplayValue('10.0.0.0/8')).toBeInTheDocument();
    expect(screen.getByDisplayValue('192.168.1.0/24')).toBeInTheDocument();
  });

  it('lets you add a CIDR row', async () => {
    renderBindings({ ipAllowlist: ['10.0.0.0/8'] });
    expect(screen.getAllByPlaceholderText(/cidr/i)).toHaveLength(1);
    await userEvent.click(screen.getByRole('button', { name: /add cidr/i }));
    expect(screen.getAllByPlaceholderText(/cidr/i)).toHaveLength(2);
  });

  it('saves the allowlist via updateLicenceIpAllowlist with reason null', async () => {
    vi.mocked(updateLicenceIpAllowlist).mockResolvedValue(undefined);
    renderBindings({ ipAllowlist: ['10.0.0.0/8'] });
    await userEvent.click(screen.getByRole('button', { name: /save allowlist/i }));
    expect(vi.mocked(updateLicenceIpAllowlist)).toHaveBeenCalledWith('lic-1', { cidrs: ['10.0.0.0/8'], reason: null });
  });

  it('sends cidrs null when the allowlist is emptied before saving', async () => {
    vi.mocked(updateLicenceIpAllowlist).mockResolvedValue(undefined);
    renderBindings({ ipAllowlist: ['10.0.0.0/8'] });
    await userEvent.click(screen.getByRole('button', { name: /remove cidr/i }));
    await userEvent.click(screen.getByRole('button', { name: /save allowlist/i }));
    expect(vi.mocked(updateLicenceIpAllowlist)).toHaveBeenCalledWith('lic-1', { cidrs: null, reason: null });
  });
});
