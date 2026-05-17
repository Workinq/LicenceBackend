import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LicenceActions } from '../components/licences/LicenceActions';

vi.mock('../api/licences', () => ({ updateLicenceStatus: vi.fn(), regenerateLicenceKey: vi.fn() }));

function renderActions(status: string) {
  const queryClient = new QueryClient();
  render(
    <QueryClientProvider client={queryClient}>
      <LicenceActions
        licence={{
          id: 'lic-1', productId: 'p', productSlug: 's', userId: 'u', userEmail: 'a@b.com',
          status, expiresAt: null, notes: null, hwidBound: false, ipAllowlist: null, createdAt: '2026-01-01T00:00:00Z',
        }}
      />
    </QueryClientProvider>,
  );
}

describe('LicenceActions', () => {
  it('offers Suspend and Revoke for an active licence', () => {
    renderActions('active');
    expect(screen.getByRole('button', { name: /^suspend$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^revoke$/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /regenerate key/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /reinstate/i })).not.toBeInTheDocument();
  });

  it('offers Reinstate and Revoke for a suspended licence', () => {
    renderActions('suspended');
    expect(screen.getByRole('button', { name: /reinstate/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^revoke$/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /regenerate key/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^suspend$/i })).not.toBeInTheDocument();
  });

  it('shows a terminal note and no action buttons for a revoked licence', () => {
    renderActions('revoked');
    expect(screen.getByText(/revoked/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /suspend|reinstate/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /regenerate key/i })).not.toBeInTheDocument();
  });
});
