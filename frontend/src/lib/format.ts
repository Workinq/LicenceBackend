export function formatPrice(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency}`;
  }
}

export function formatDate(value: string): string {
  return new Date(value).toLocaleDateString();
}

export function formatDateTime(value: string): string {
  return new Date(value).toLocaleString();
}

export function formatRelative(value: string | Date, now: Date = new Date()): string {
  const date = typeof value === 'string' ? new Date(value) : value;
  const diffSeconds = Math.round((now.getTime() - date.getTime()) / 1000);
  const abs = Math.abs(diffSeconds);
  if (abs < 60) return diffSeconds <= 0 ? 'just now' : `${diffSeconds}s ago`;
  if (abs < 3600) return `${Math.floor(diffSeconds / 60)}m ago`;
  if (abs < 86400) return `${Math.floor(diffSeconds / 3600)}h ago`;
  if (abs < 604800) return `${Math.floor(diffSeconds / 86400)}d ago`;
  return date.toLocaleDateString();
}
