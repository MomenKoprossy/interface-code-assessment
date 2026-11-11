import { useRef, useState, type FormEvent } from 'react';
import { flushSync } from 'react-dom';
import { Link } from 'react-router-dom';
import { errorMessage, isApiError } from '../../api/problem';
import type { EmployeeInput } from '../../api/types';
import { Alert } from '../../shared/components/Alert';
import { Button, buttonClass } from '../../shared/components/Button';
import { MoneyField } from '../../shared/components/MoneyField';
import { SelectField } from '../../shared/components/SelectField';
import { TextField } from '../../shared/components/TextField';
import { todayUtc } from '../../shared/format';
import { mapServerErrors, newPayItemDraft, removeItemErrors, toEmployeeInput } from './mapping';
import {
  LIMITS,
  validateEmployee,
  validateField,
  type EmployeeFormValues,
  type FormErrors,
  type PayItemDraft,
  type PayItemList,
} from './validation';

type SimpleField = Exclude<keyof EmployeeFormValues, PayItemList>;

interface EmployeeFormProps {
  initialValues: EmployeeFormValues;
  submitLabel: string;
  /** Where Cancel goes: the list, with its filters and page. */
  cancelTo?: string;
  /** Throws an ApiError on failure; 400/409 field errors are mapped onto the form. */
  onSubmit: (input: EmployeeInput) => Promise<void>;
  /** `YYYY-MM-DD` (UTC); injectable so tests don't depend on the real clock. */
  today?: string;
  /** Shown on the money inputs; the values themselves are plain decimals. */
  currency?: string;
}

function withError(errors: FormErrors, path: string, message: string | undefined): FormErrors {
  if (errors[path] === message) return errors;
  const next = { ...errors };
  if (message) next[path] = message;
  else delete next[path];
  return next;
}

export function EmployeeForm({
  initialValues,
  submitLabel,
  cancelTo = '/employees',
  onSubmit,
  today = todayUtc(),
  currency,
}: EmployeeFormProps) {
  const formRef = useRef<HTMLFormElement>(null);
  const [values, setValues] = useState(initialValues);
  const [errors, setErrors] = useState<FormErrors>({});
  const [alerts, setAlerts] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);

  /** Applies a change; a field already showing an error is re-checked live so the message clears once fixed. */
  function change(next: EmployeeFormValues, path: string) {
    setValues(next);
    if (errors[path]) setErrors((current) => withError(current, path, validateField(path, next, today)));
  }

  function blur(path: string) {
    const list = /^(allowances|deductions)\[\d+\]\.name$/.exec(path)?.[1];
    if (!list) {
      setErrors((current) => withError(current, path, validateField(path, values, today)));
      return;
    }
    // Item names are checked against each other: refresh this one and every other name error already on screen.
    const fresh = validateEmployee(values, today);
    setErrors((current) => {
      const next = { ...current };
      const duplicates = Object.keys(fresh).filter((key) => fresh[key] === 'Name must be unique in this list.');
      for (const key of new Set([path, ...Object.keys(current), ...duplicates])) {
        if (!key.startsWith(`${list}[`) || !key.endsWith('.name')) continue;
        if (fresh[key]) next[key] = fresh[key];
        else delete next[key];
      }
      return next;
    });
  }

  const fieldProps = (name: SimpleField) => ({
    value: values[name],
    error: errors[name],
    onChange: (value: string) => change({ ...values, [name]: value }, name),
    onBlur: () => blur(name),
  });

  function changeItem(list: PayItemList, index: number, key: 'name' | 'amount', value: string) {
    const items = values[list].map((item, i) => (i === index ? { ...item, [key]: value } : item));
    change({ ...values, [list]: items }, `${list}[${index}].${key}`);
  }

  function addItem(list: PayItemList) {
    setValues({ ...values, [list]: [...values[list], newPayItemDraft()] });
  }

  function removeItem(list: PayItemList, index: number) {
    const next = { ...values, [list]: values[list].filter((_, i) => i !== index) };
    setValues(next);
    // Re-check the rows still showing errors: removing a duplicate clears the other row's "unique" error.
    const fresh = validateEmployee(next, today);
    setErrors((current) => {
      const shifted = removeItemErrors(current, list, index);
      for (const path of Object.keys(shifted)) {
        if (!path.startsWith(`${list}[`)) continue;
        if (fresh[path]) shifted[path] = fresh[path];
        else delete shifted[path];
      }
      return shifted;
    });
  }

  function showErrors(fieldErrors: FormErrors, messages: string[]) {
    flushSync(() => {
      setErrors(fieldErrors);
      setAlerts(messages);
      // Re-enable the fieldset first: a disabled input can't take focus.
      setSaving(false);
    });
    formRef.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus();
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const found = validateEmployee(values, today);
    if (Object.keys(found).length > 0) {
      showErrors(found, []);
      return;
    }

    setSaving(true);
    setAlerts([]);
    try {
      await onSubmit(toEmployeeInput(values));
      setSaving(false);
    } catch (error) {
      const hasFieldErrors = isApiError(error) && Object.keys(error.fieldErrors).length > 0;
      if (hasFieldErrors && (error.status === 400 || error.status === 409)) {
        const { fields, unmapped } = mapServerErrors(error.fieldErrors, values);
        showErrors(fields, unmapped);
      } else {
        showErrors({}, [errorMessage(error)]);
      }
    }
  }

  return (
    <form ref={formRef} className="employee-form" onSubmit={handleSubmit} noValidate aria-busy={saving}>
      {alerts.length > 0 && (
        <Alert title="The employee couldn't be saved">
          <ul className="alert__list">
            {alerts.map((message) => (
              <li key={message}>{message}</li>
            ))}
          </ul>
        </Alert>
      )}

      <fieldset className="card form-section" disabled={saving}>
        <legend className="form-section__title">Details</legend>
        <div className="form-grid">
          <TextField label="Full name" required maxLength={LIMITS.textMax} autoComplete="off" {...fieldProps('fullName')} />
          <TextField label="Email" type="email" required maxLength={LIMITS.emailMax} autoComplete="off" {...fieldProps('email')} />
          <TextField label="Job title" required maxLength={LIMITS.textMax} {...fieldProps('jobTitle')} />
          <TextField label="Department" required maxLength={LIMITS.textMax} {...fieldProps('department')} />
          <TextField label="Hire date" type="date" required min={LIMITS.earliestHireDate} max={today} {...fieldProps('hireDate')} />
          <MoneyField label="Base monthly salary" required suffix={currency} {...fieldProps('baseMonthlySalary')} />
          <SelectField
            label="Status"
            required
            options={[
              { value: 'Active', label: 'Active' },
              { value: 'Inactive', label: 'Inactive' },
            ]}
            {...fieldProps('status')}
          />
        </div>
      </fieldset>

      {(['allowances', 'deductions'] as const).map((list) => (
        <PayItemsSection
          key={list}
          list={list}
          items={values[list]}
          errors={errors}
          disabled={saving}
          onChange={(index, key, value) => changeItem(list, index, key, value)}
          onBlur={(index, key) => blur(`${list}[${index}].${key}`)}
          onAdd={() => addItem(list)}
          onRemove={(index) => removeItem(list, index)}
          currency={currency}
        />
      ))}

      <div className="form-actions">
        <Link to={cancelTo} className={buttonClass('secondary')}>
          Cancel
        </Link>
        <Button type="submit" disabled={saving}>
          {saving ? 'Saving…' : submitLabel}
        </Button>
      </div>
    </form>
  );
}

const sectionText: Record<PayItemList, { title: string; singular: string; hint: string }> = {
  allowances: { title: 'Allowances', singular: 'Allowance', hint: 'Fixed monthly amounts added to the base salary.' },
  deductions: { title: 'Other deductions', singular: 'Deduction', hint: 'Recurring monthly amounts taken after tax.' },
};

interface PayItemsSectionProps {
  list: PayItemList;
  items: PayItemDraft[];
  errors: FormErrors;
  disabled: boolean;
  onChange: (index: number, key: 'name' | 'amount', value: string) => void;
  onBlur: (index: number, key: 'name' | 'amount') => void;
  onAdd: () => void;
  onRemove: (index: number) => void;
  currency?: string;
}

function PayItemsSection({ list, items, errors, disabled, onChange, onBlur, onAdd, onRemove, currency }: PayItemsSectionProps) {
  const text = sectionText[list];
  const full = items.length >= LIMITS.itemsMax;
  const sectionRef = useRef<HTMLFieldSetElement>(null);
  const addRef = useRef<HTMLButtonElement>(null);

  /** The clicked button unmounts, so focus moves to the Remove button now in its place, or to Add when none is left. */
  function remove(index: number) {
    flushSync(() => onRemove(index));
    const removes = sectionRef.current?.querySelectorAll<HTMLElement>('.pay-item__remove') ?? [];
    (removes[Math.min(index, removes.length - 1)] ?? addRef.current)?.focus();
  }

  return (
    <fieldset ref={sectionRef} className="card form-section" disabled={disabled}>
      <legend className="form-section__title">{text.title}</legend>
      <p className="form-section__hint">{text.hint}</p>
      {errors[list] && (
        <p className="field__error" role="alert">
          {errors[list]}
        </p>
      )}
      {items.length === 0 && <p className="form-section__empty">None.</p>}
      {items.length > 0 && (
        // Visible column labels; each input keeps its own (screen-reader) label.
        <div className="pay-item pay-item--header" aria-hidden="true">
          <span className="field__label">
            Name<span aria-hidden="true"> *</span>
          </span>
          <span className="field__label">
            Amount (monthly)<span aria-hidden="true"> *</span>
          </span>
          <span className={`${buttonClass('ghost', true)} pay-item__spacer`}>Remove</span>
        </div>
      )}
      {items.map((item, index) => (
        <div className="pay-item" key={item.key}>
          <TextField
            label={`${text.singular} ${index + 1} name`}
            required
            placeholder="Name"
            maxLength={LIMITS.itemNameMax}
            value={item.name}
            error={errors[`${list}[${index}].name`]}
            onChange={(value) => onChange(index, 'name', value)}
            onBlur={() => onBlur(index, 'name')}
            className="pay-item__name"
          />
          <MoneyField
            label={`${text.singular} ${index + 1} amount`}
            required
            suffix={currency}
            value={item.amount}
            error={errors[`${list}[${index}].amount`]}
            onChange={(value) => onChange(index, 'amount', value)}
            onBlur={() => onBlur(index, 'amount')}
            className="pay-item__amount"
          />
          <Button
            variant="ghost"
            small
            onClick={() => remove(index)}
            className="pay-item__remove"
            aria-label={`Remove ${text.singular.toLowerCase()} ${index + 1}`}
          >
            Remove
          </Button>
        </div>
      ))}
      <Button ref={addRef} variant="secondary" small onClick={onAdd} disabled={full}>
        <span aria-hidden="true">+</span> Add {text.singular.toLowerCase()}
      </Button>
      {full && <p className="form-section__hint">You can add at most {LIMITS.itemsMax}.</p>}
    </fieldset>
  );
}
