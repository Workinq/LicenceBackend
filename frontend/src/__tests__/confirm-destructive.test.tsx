import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ConfirmDestructive } from '../components/ConfirmDestructive';

describe('ConfirmDestructive', () => {
  it('opens the dialog from the trigger and shows the title, description, and confirm label', async () => {
    render(
      <ConfirmDestructive
        trigger={<button type="button">Revoke</button>}
        title="Revoke this licence?"
        description="The client will stop validating immediately."
        confirmLabel="Revoke licence"
        onConfirm={() => {}}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Revoke' }));
    expect(await screen.findByText('Revoke this licence?')).toBeInTheDocument();
    expect(screen.getByText('The client will stop validating immediately.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Revoke licence' })).toBeInTheDocument();
  });

  it('calls onConfirm when the confirm button is clicked', async () => {
    const onConfirm = vi.fn();
    render(
      <ConfirmDestructive
        trigger={<button type="button">Revoke</button>}
        title="Revoke this licence?"
        description="x"
        confirmLabel="Revoke licence"
        onConfirm={onConfirm}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Revoke' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Revoke licence' }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it('does not call onConfirm when cancelled', async () => {
    const onConfirm = vi.fn();
    render(
      <ConfirmDestructive
        trigger={<button type="button">Revoke</button>}
        title="t"
        description="x"
        confirmLabel="Revoke licence"
        onConfirm={onConfirm}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: 'Revoke' }));
    await userEvent.click(await screen.findByRole('button', { name: /cancel/i }));
    expect(onConfirm).not.toHaveBeenCalled();
  });
});
