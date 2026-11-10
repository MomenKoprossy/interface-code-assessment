import { useId, type InputHTMLAttributes } from 'react';

type NativeInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'onChange' | 'value' | 'id'>;

export interface TextFieldProps extends NativeInputProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  error?: string;
  /** A short unit shown inside the end of the input, e.g. the currency code. Decorative: the label carries the meaning. */
  suffix?: string;
}

export function TextField({ label, value, onChange, error, suffix, className, ...input }: TextFieldProps) {
  const id = useId();
  const errorId = error ? `${id}-error` : undefined;
  const control = (
    <input
      {...input}
      id={id}
      className="field__control"
      value={value}
      onChange={(event) => onChange(event.target.value)}
      aria-invalid={error ? true : undefined}
      aria-describedby={errorId}
    />
  );

  return (
    <div className={`field${error ? ' field--invalid' : ''}${className ? ` ${className}` : ''}`}>
      <label htmlFor={id} className="field__label">
        {label}
        {input.required && <span aria-hidden="true"> *</span>}
      </label>
      {suffix ? (
        <div className="field__affix">
          {control}
          <span className="field__suffix" aria-hidden="true">
            {suffix}
          </span>
        </div>
      ) : (
        control
      )}
      {error && (
        <p id={errorId} className="field__error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
