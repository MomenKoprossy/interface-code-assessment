import { TextField, type TextFieldProps } from './TextField';

/** A text input for amounts: a decimal keypad on mobile, and the raw text is kept so validation sees what was typed. */
export function MoneyField(props: TextFieldProps) {
  return <TextField inputMode="decimal" autoComplete="off" placeholder="0.00" {...props} />;
}
