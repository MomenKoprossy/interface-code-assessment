import { useId, type InputHTMLAttributes } from 'react';

type NativeInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'onChange' | 'value' | 'id'>;

export interface TextFieldProps extends NativeInputProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  /** Visually hides the label (still read by screen readers), for compact rows. */
  hideLabel?: boolean;
}

export function TextField({ label, value, onChange, error, hideLabel, className, ...input }: TextFieldProps) {
  const id = useId();
  const errorId = error ? `${id}-error` : undefined;

  return (
    <div className={`field${error ? ' field--invalid' : ''}${className ? ` ${className}` : ''}`}>
      <label htmlFor={id} className={hideLabel ? 'visually-hidden' : 'field__label'}>
        {label}
        {input.required && <span aria-hidden="true"> *</span>}
      </label>
      <input
        {...input}
        id={id}
        className="field__control"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={error ? true : undefined}
        aria-describedby={errorId}
      />
      {error && (
        <p id={errorId} className="field__error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
