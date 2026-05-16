const ALPHABET = 'ABCDEFGHJKMNPQRSTVWXYZ0123456789abcdefghjkmnpqrstvwxyz';

export function generatePassword(length = 24): string {
  if (length <= 0) throw new Error('length must be positive');
  const buf = new Uint32Array(length);
  crypto.getRandomValues(buf);
  let out = '';
  for (let i = 0; i < length; i++) {
    out += ALPHABET[buf[i] % ALPHABET.length];
  }
  return out;
}
