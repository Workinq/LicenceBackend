import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatusPill } from '../components/StatusPill';

describe('StatusPill', () => {
  it('renders the status text', () => {
    render(<StatusPill status="active" />);
    expect(screen.getByText('active')).toBeInTheDocument();
  });

  it('applies the active palette classes', () => {
    render(<StatusPill status="active" />);
    const el = screen.getByText('active');
    expect(el.className).toContain('bg-status-active-bg');
    expect(el.className).toContain('text-status-active-fg');
  });

  it('applies the suspended palette classes', () => {
    render(<StatusPill status="suspended" />);
    const el = screen.getByText('suspended');
    expect(el.className).toContain('bg-status-suspended-bg');
    expect(el.className).toContain('text-status-suspended-fg');
  });

  it('applies the revoked palette classes', () => {
    render(<StatusPill status="revoked" />);
    const el = screen.getByText('revoked');
    expect(el.className).toContain('bg-status-revoked-bg');
    expect(el.className).toContain('text-status-revoked-fg');
  });

  it('falls back to a neutral palette for unknown statuses', () => {
    render(<StatusPill status="weird" />);
    const el = screen.getByText('weird');
    expect(el.className).toContain('bg-surface-sunken');
  });
});
