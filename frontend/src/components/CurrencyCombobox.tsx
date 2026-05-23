import { useQuery } from '@tanstack/react-query';
import { Combobox, type ComboboxOption } from '@/components/Combobox';
import { fetchCurrencyList, FALLBACK_CURRENCIES } from '@/api/currencies';

interface CurrencyComboboxProps {
  value: string;
  onChange: (value: string) => void;
  id?: string;
  disabled?: boolean;
}

export function CurrencyCombobox({ value, onChange, id, disabled }: Readonly<CurrencyComboboxProps>) {
  const query = useQuery({ queryKey: ['currencies'], queryFn: fetchCurrencyList, staleTime: Infinity });

  const base: ComboboxOption[] = query.data
    ? Object.entries(query.data)
        .filter(([code]) => /^[a-z]{3}$/.test(code))
        .map(([code, name]) => ({ value: code.toUpperCase(), label: `${code.toUpperCase()} - ${name}` }))
        .sort((a, b) => a.value.localeCompare(b.value))
    : FALLBACK_CURRENCIES.map((c) => ({ value: c.code, label: `${c.code} - ${c.name}` }));

  const options = value && !base.some((o) => o.value === value) ? [{ value, label: value }, ...base] : base;

  return (
    <Combobox
      id={id}
      options={options}
      value={value}
      onChange={onChange}
      placeholder="Choose a currency..."
      searchPlaceholder="Search currencies"
      emptyText="No matching currency"
      disabled={disabled}
    />
  );
}
