const onlyDigits = (value: string, limit: number) => value.replace(/\D/g, '').slice(0, limit);

export const formatCpf = (value: string) => {
  const digits = onlyDigits(value, 11);
  return digits
    .replace(/(\d{3})(\d)/, '$1.$2')
    .replace(/(\d{3})(\d)/, '$1.$2')
    .replace(/(\d{3})(\d{1,2})$/, '$1-$2');
};

export const formatRg = (value: string) => {
  const characters = value.toUpperCase().replace(/[^0-9X]/g, '').slice(0, 9);
  return characters
    .replace(/(\w{2})(\w)/, '$1.$2')
    .replace(/(\w{3})(\w)/, '$1.$2')
    .replace(/(\w{3})(\w{1,2})$/, '$1-$2');
};

export const formatPhone = (value: string) => {
  const digits = onlyDigits(value, 11);
  if (digits.length <= 2) return digits ? `(${digits}` : '';
  if (digits.length <= 6) return `(${digits.slice(0, 2)}) ${digits.slice(2)}`;
  if (digits.length <= 10) return `(${digits.slice(0, 2)}) ${digits.slice(2, 6)}-${digits.slice(6)}`;
  return `(${digits.slice(0, 2)}) ${digits.slice(2, 7)}-${digits.slice(7)}`;
};

export const formatCity = (value: string) => value
  .replace(/[^\p{L}\s.'-]/gu, '')
  .replace(/\s{2,}/g, ' ')
  .slice(0, 100);

export const formatCurrencyInput = (value: string | number) => {
  const cents = onlyDigits(String(value), 12);
  const amount = Number(cents || '0') / 100;
  return amount.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
};

export const currencyInputFromNumber = (value: number) => formatCurrencyInput(Math.round(value * 100));

export const parseCurrencyInput = (value: string) => Number(onlyDigits(value, 12) || '0') / 100;
