import { useId, type SelectHTMLAttributes } from 'react';

export interface SelectOption {
  value: string;
  label: string;
}

type NativeSelectProps = Omit<SelectHTMLAttributes<HTMLSelectElement>, 'onChange' | 'value' | 'id'>;

interface SelectFieldProps extends NativeSelectProps {
  label: string;
  value: string;
  options: SelectOption[];
  onChange: (value: string) => void;
  error?: string;
}

export function SelectField({ label, value, options, onChange, error, className, ...select }: SelectFieldProps) {
  const id = useId();
  const errorId = error ? `${id}-error` : undefined;

  return (
    <div className={`field${error ? ' field--invalid' : ''}${className ? ` ${className}` : ''}`}>
      <label htmlFor={id} className="field__label">
        {label}
        {select.required && <span aria-hidden="true"> *</span>}
      </label>
      <select
        {...select}
        id={id}
        className="field__control"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={error ? true : undefined}
        aria-describedby={errorId}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
      {error && (
        <p id={errorId} className="field__error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
