import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SecretRevealOnce } from '../components/SecretRevealOnce';

const writeText = vi.fn().mockResolvedValue(undefined);

beforeEach(() => {
  Object.assign(navigator, { clipboard: { writeText } });
  writeText.mockClear();
});
afterEach(() => {
  vi.restoreAllMocks();
});

describe('SecretRevealOnce', () => {
  it('renders the secret value and a one-time warning', () => {
    render(<SecretRevealOnce label="Licence key" value="LK-abc-123" />);
    expect(screen.getByText('LK-abc-123')).toBeInTheDocument();
    expect(screen.getByText(/will not be able to see this/i)).toBeInTheDocument();
  });

  it('copies the value when the copy button is clicked', async () => {
    render(<SecretRevealOnce label="Licence key" value="LK-abc-123" />);
    await userEvent.click(screen.getByRole('button', { name: /copy/i }));
    expect(writeText).toHaveBeenCalledWith('LK-abc-123');
  });
});
