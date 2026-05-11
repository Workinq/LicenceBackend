import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LicenceKey } from '../components/LicenceKey';

const writeText = vi.fn().mockResolvedValue(undefined);

beforeEach(() => {
  Object.assign(navigator, { clipboard: { writeText } });
  writeText.mockClear();
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('LicenceKey', () => {
  it('renders the value', () => {
    render(<LicenceKey value="ABC-123" />);
    expect(screen.getByText('ABC-123')).toBeInTheDocument();
  });

  it('copies the value to the clipboard when the copy button is clicked', async () => {
    render(<LicenceKey value="ABC-123" />);
    await userEvent.click(screen.getByRole('button', { name: /copy/i }));
    expect(writeText).toHaveBeenCalledWith('ABC-123');
  });
});
