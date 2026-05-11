import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ImagePickerButton } from '../components/ImagePickerButton';

describe('ImagePickerButton', () => {
  it('renders a labelled button', () => {
    render(<ImagePickerButton onSelect={() => {}} label="Upload image" />);
    expect(screen.getByText('Upload image')).toBeInTheDocument();
  });

  it('calls onSelect with the chosen file', async () => {
    const onSelect = vi.fn();
    render(<ImagePickerButton onSelect={onSelect} label="Upload image" />);
    const input = screen.getByLabelText('Upload image');
    const file = new File(['x'], 'logo.png', { type: 'image/png' });
    await userEvent.upload(input, file);
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect((onSelect.mock.calls[0][0] as File).name).toBe('logo.png');
  });

  it('uses a default label when none is given', () => {
    render(<ImagePickerButton onSelect={() => {}} />);
    expect(screen.getByText(/choose image/i)).toBeInTheDocument();
  });
});
