const CURRENCY_LIST_URL =
  'https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies.min.json';

/** A small fallback used if the CDN fetch fails or while it is loading. */
export const FALLBACK_CURRENCIES: readonly { code: string; name: string }[] = [
  { code: 'USD', name: 'United States Dollar' },
  { code: 'EUR', name: 'Euro' },
  { code: 'GBP', name: 'Pound Sterling' },
  { code: 'JPY', name: 'Japanese Yen' },
  { code: 'AUD', name: 'Australian Dollar' },
  { code: 'CAD', name: 'Canadian Dollar' },
  { code: 'CHF', name: 'Swiss Franc' },
  { code: 'CNY', name: 'Chinese Yuan' },
  { code: 'INR', name: 'Indian Rupee' },
  { code: 'NZD', name: 'New Zealand Dollar' },
  { code: 'BRL', name: 'Brazilian Real' },
  { code: 'ZAR', name: 'South African Rand' },
];

/** Fetches the currency-code to name map from the public CDN. Keys are lowercase. */
export async function fetchCurrencyList(): Promise<Record<string, string>> {
  const res = await fetch(CURRENCY_LIST_URL);
  if (!res.ok) throw new Error(`Currency list fetch failed: ${res.status}`);
  return (await res.json()) as Record<string, string>;
}
