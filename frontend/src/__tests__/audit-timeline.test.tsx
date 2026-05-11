import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Activity } from 'lucide-react';
import { AuditTimeline, type AuditEvent } from '../components/AuditTimeline';

const events: AuditEvent[] = [
  { id: 'e1', icon: Activity, title: 'Suspended -> Active', meta: 'by admin@example.com', timestamp: '2026-01-02T10:00:00Z' },
  { id: 'e2', icon: Activity, title: 'Active -> Suspended', meta: 'by admin@example.com', timestamp: '2026-01-01T09:00:00Z' },
];

describe('AuditTimeline', () => {
  it('renders an entry per event with its title and meta', () => {
    render(<AuditTimeline events={events} isLoading={false} isError={false} />);
    expect(screen.getByText('Suspended -> Active')).toBeInTheDocument();
    expect(screen.getByText('Active -> Suspended')).toBeInTheDocument();
    expect(screen.getAllByText('by admin@example.com')).toHaveLength(2);
  });

  it('shows a loading skeleton when isLoading', () => {
    const { container } = render(<AuditTimeline events={[]} isLoading isError={false} />);
    expect(container.querySelector('[data-slot="skeleton"]')).not.toBeNull();
  });

  it('shows an error message when isError', () => {
    render(<AuditTimeline events={[]} isLoading={false} isError errorText="Could not load history." />);
    expect(screen.getByText('Could not load history.')).toBeInTheDocument();
  });

  it('shows the empty text when there are no events and not loading or erroring', () => {
    render(<AuditTimeline events={[]} isLoading={false} isError={false} emptyText="No history yet." />);
    expect(screen.getByText('No history yet.')).toBeInTheDocument();
  });
});
