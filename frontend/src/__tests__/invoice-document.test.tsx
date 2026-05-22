import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InvoiceDocument } from '../components/InvoiceDocument';
import type { InvoiceResponse } from '../api/generated/api.schemas';

function makeInvoice(over: Partial<InvoiceResponse> = {}): InvoiceResponse {
  return {
    orderId: 'ord-1',
    invoiceNumber: 'INV-2026-0001',
    issuedAt: '2026-05-22T10:00:00Z',
    status: 'paid',
    seller: {
      name: 'Acme Software Ltd',
      addressLine1: '1 Seller Way',
      addressLine2: 'Suite 200',
      city: 'Sellerton',
      region: 'SR',
      postalCode: 'S12 3AB',
      country: 'UK',
    },
    buyer: {
      contactEmail: 'buyer@example.com',
      name: 'Buyer Co',
      addressLine1: '2 Buyer Road',
      addressLine2: null,
      city: 'Buyerville',
      region: 'BR',
      postalCode: 'B45 6CD',
      country: 'UK',
    },
    lineItems: [
      { licenceId: 'lic-1', productId: 'p-1', productName: 'Widget Pro', productSlug: 'widget-pro', label: 'Team A', unitPrice: 49.5, currency: 'USD' },
      { licenceId: 'lic-2', productId: 'p-2', productName: 'Gizmo Lite', productSlug: 'gizmo-lite', label: null, unitPrice: null, currency: 'USD' },
    ],
    totals: [{ currency: 'USD', amount: 49.5 }],
    ...over,
  };
}

const originalPrint = window.print;

beforeEach(() => {
  window.print = vi.fn();
});

afterEach(() => {
  window.print = originalPrint;
  vi.restoreAllMocks();
});

describe('InvoiceDocument', () => {
  it('renders the invoice header with number and status', () => {
    render(<InvoiceDocument invoice={makeInvoice()} />);
    expect(screen.getByRole('heading', { name: /^invoice$/i })).toBeInTheDocument();
    expect(screen.getByText('INV-2026-0001')).toBeInTheDocument();
    expect(screen.getByText(/status:\s*paid/i)).toBeInTheDocument();
  });

  it('renders the seller block', () => {
    render(<InvoiceDocument invoice={makeInvoice()} />);
    expect(screen.getByText('Acme Software Ltd')).toBeInTheDocument();
    expect(screen.getByText('1 Seller Way')).toBeInTheDocument();
    expect(screen.getByText('Suite 200')).toBeInTheDocument();
    expect(screen.getByText('Sellerton, SR, S12 3AB')).toBeInTheDocument();
  });

  it('shows the buyer name and email when both are present', () => {
    render(<InvoiceDocument invoice={makeInvoice()} />);
    expect(screen.getByText('Buyer Co')).toBeInTheDocument();
    expect(screen.getByText('buyer@example.com')).toBeInTheDocument();
    expect(screen.getByText('2 Buyer Road')).toBeInTheDocument();
  });

  it('falls back to contact email as the bill-to header when name is null', () => {
    render(<InvoiceDocument invoice={makeInvoice({ buyer: { contactEmail: 'solo@example.com', name: null, addressLine1: null, addressLine2: null, city: null, region: null, postalCode: null, country: null } })} />);
    expect(screen.getByText('solo@example.com')).toBeInTheDocument();
  });

  it('renders one row per line item with product name and slug', () => {
    render(<InvoiceDocument invoice={makeInvoice()} />);
    expect(screen.getByText('Widget Pro')).toBeInTheDocument();
    expect(screen.getByText('widget-pro')).toBeInTheDocument();
    expect(screen.getByText('Gizmo Lite')).toBeInTheDocument();
    expect(screen.getByText('gizmo-lite')).toBeInTheDocument();
  });

  it('renders the label or a dash placeholder per line item', () => {
    render(<InvoiceDocument invoice={makeInvoice()} />);
    expect(screen.getByText('Team A')).toBeInTheDocument();
    expect(screen.getByText('-')).toBeInTheDocument();
  });

  it('renders Free when the line item has no unit price', () => {
    render(<InvoiceDocument invoice={makeInvoice()} />);
    expect(screen.getByText('Free')).toBeInTheDocument();
  });

  it('renders the total in the given currency', () => {
    render(<InvoiceDocument invoice={makeInvoice()} />);
    const table = screen.getByRole('table');
    expect(within(table).getByText(/\$49\.50/)).toBeInTheDocument();
  });

  it('calls window.print when the Print button is clicked', async () => {
    render(<InvoiceDocument invoice={makeInvoice()} />);
    await userEvent.click(screen.getByRole('button', { name: /print/i }));
    expect(window.print).toHaveBeenCalledTimes(1);
  });

  it('omits the address block when no address fields are set', () => {
    render(
      <InvoiceDocument
        invoice={makeInvoice({
          buyer: {
            contactEmail: 'noaddr@example.com',
            name: 'No Address Co',
            addressLine1: null,
            addressLine2: null,
            city: null,
            region: null,
            postalCode: null,
            country: null,
          },
        })}
      />,
    );
    expect(screen.getByText('No Address Co')).toBeInTheDocument();
    expect(screen.queryByText('2 Buyer Road')).not.toBeInTheDocument();
  });
});
