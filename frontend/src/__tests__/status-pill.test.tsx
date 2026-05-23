import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatusPill } from '../components/StatusPill';

describe('StatusPill', () => {
  it('renders the status text', () => {
    render(<StatusPill status="active" />);
    expect(screen.getByText('active')).toBeInTheDocument();
  });

  it('renders a colored dot for active', () => {
    const { container } = render(<StatusPill status="active" />);
    const dot = container.querySelector('span[aria-hidden]');
    expect(dot).not.toBeNull();
    expect(dot?.getAttribute('style')).toContain('#16a34a');
  });

  it('renders a colored dot for suspended', () => {
    const { container } = render(<StatusPill status="suspended" />);
    const dot = container.querySelector('span[aria-hidden]');
    expect(dot?.getAttribute('style')).toContain('#d97706');
  });

  it('renders a colored dot for revoked', () => {
    const { container } = render(<StatusPill status="revoked" />);
    const dot = container.querySelector('span[aria-hidden]');
    expect(dot?.getAttribute('style')).toContain('#dc2626');
  });

  it('falls back to a neutral dot for unknown statuses', () => {
    const { container } = render(<StatusPill status="weird" />);
    const dot = container.querySelector('span[aria-hidden]');
    expect(dot?.getAttribute('style')).toContain('#71717a');
  });
});
