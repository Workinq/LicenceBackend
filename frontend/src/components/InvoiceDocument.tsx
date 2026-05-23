import { Button } from '@/components/ui/button';
import { formatDateTime, formatPrice } from '@/lib/format';
import type { InvoiceResponse } from '@/api/generated/api.schemas';

export function InvoiceDocument({ invoice }: Readonly<{ invoice: InvoiceResponse }>) {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between print:hidden">
        <h1 className="font-display text-2xl font-semibold text-ink">Invoice</h1>
        <Button variant="outline" size="sm" onClick={() => globalThis.print()}>
          Print / Save as PDF
        </Button>
      </div>

      <div className="rounded-lg border border-border bg-white p-8 text-ink print:border-0 print:p-0">
        <div className="flex flex-wrap items-start justify-between gap-6">
          <div>
            <div className="text-xs uppercase tracking-wide text-ink-subtle">From</div>
            <div className="mt-1 font-medium">{invoice.seller.name}</div>
            <AddressBlock
              line1={invoice.seller.addressLine1}
              line2={invoice.seller.addressLine2}
              city={invoice.seller.city}
              region={invoice.seller.region}
              postalCode={invoice.seller.postalCode}
              country={invoice.seller.country}
            />
          </div>
          <div className="text-right">
            <div className="font-display text-xl font-semibold">{invoice.invoiceNumber}</div>
            <div className="text-sm text-ink-muted">Issued {formatDateTime(invoice.issuedAt)}</div>
            <div className="mt-1 text-xs uppercase tracking-wide text-ink-subtle">
              Status: {invoice.status}
            </div>
          </div>
        </div>

        <div className="mt-6">
          <div className="text-xs uppercase tracking-wide text-ink-subtle">Bill to</div>
          <div className="mt-1 text-sm">{invoice.buyer.name ?? invoice.buyer.contactEmail}</div>
          {invoice.buyer.name && (
            <div className="text-sm text-ink-muted">{invoice.buyer.contactEmail}</div>
          )}
          <AddressBlock
            line1={invoice.buyer.addressLine1}
            line2={invoice.buyer.addressLine2}
            city={invoice.buyer.city}
            region={invoice.buyer.region}
            postalCode={invoice.buyer.postalCode}
            country={invoice.buyer.country}
          />
        </div>

        <table className="mt-6 w-full text-sm">
          <thead>
            <tr className="border-b border-border text-left text-xs uppercase tracking-wide text-ink-subtle">
              <th className="py-2">Product</th>
              <th className="py-2">Label</th>
              <th className="py-2 text-right">Amount</th>
            </tr>
          </thead>
          <tbody>
            {invoice.lineItems.map((item) => (
              <tr key={item.licenceId} className="border-b border-border/60">
                <td className="py-2">
                  <div className="font-medium">{item.productName}</div>
                  <div className="font-mono text-[11px] text-ink-muted">{item.productSlug}</div>
                </td>
                <td className="py-2 text-ink-muted">{item.label ?? '-'}</td>
                <td className="py-2 text-right tabular-nums">
                  {item.unitPrice == null ? 'Free' : formatPrice(item.unitPrice, item.currency)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>

        <div className="mt-4 flex flex-col items-end gap-1">
          {invoice.totals.map((total) => (
            <div key={total.currency} className="text-sm">
              <span className="text-ink-muted">Total </span>
              <span className="font-semibold tabular-nums">
                {formatPrice(total.amount, total.currency)}
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function AddressBlock(props: Readonly<{
  line1?: string | null;
  line2?: string | null;
  city?: string | null;
  region?: string | null;
  postalCode?: string | null;
  country?: string | null;
}>) {
  const lines = [
    { tag: 'line1', value: props.line1 },
    { tag: 'line2', value: props.line2 },
    { tag: 'locality', value: [props.city, props.region, props.postalCode].filter(Boolean).join(', ') },
    { tag: 'country', value: props.country },
  ].filter((line) => line.value && line.value.trim().length > 0);

  if (lines.length === 0) return null;
  return (
    <div className="mt-1 text-sm text-ink-muted">
      {lines.map((line) => (
        <div key={line.tag}>{line.value}</div>
      ))}
    </div>
  );
}
