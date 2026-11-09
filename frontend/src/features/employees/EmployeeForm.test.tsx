import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../api/problem';
import type { EmployeeInput } from '../../api/types';
import { renderRoute } from '../../test/render';
import { EmployeeForm } from './EmployeeForm';
import { emptyFormValues } from './mapping';

const TODAY = '2026-11-09';

function renderForm(onSubmit: (input: EmployeeInput) => Promise<void> = vi.fn().mockResolvedValue(undefined)) {
  renderRoute(<EmployeeForm initialValues={emptyFormValues()} submitLabel="Add employee" onSubmit={onSubmit} today={TODAY} />);
  return { user: userEvent.setup(), onSubmit };
}

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/full name/i), 'Ada Lovelace');
  await user.type(screen.getByLabelText(/^email/i), 'ada@example.com');
  await user.type(screen.getByLabelText(/job title/i), 'Engineer');
  await user.type(screen.getByLabelText(/department/i), 'R&D');
  // Date inputs reject partial text in jsdom, so set the whole value at once.
  fireEvent.change(screen.getByLabelText(/hire date/i), { target: { value: '2020-01-15' } });
  await user.type(screen.getByLabelText(/base monthly salary/i), '5000');
}

describe('EmployeeForm', () => {
  it('shows a client error on blur and links it to the input', async () => {
    const { user } = renderForm();
    const name = screen.getByLabelText(/full name/i);

    await user.click(name);
    await user.tab();

    expect(name).toHaveAccessibleDescription('Full name is required.');
    expect(name).toHaveAttribute('aria-invalid', 'true');
  });

  it('clears the error as soon as the field becomes valid', async () => {
    const { user } = renderForm();
    const salary = screen.getByLabelText(/base monthly salary/i);
    await user.type(salary, '12.345');
    await user.tab();
    expect(screen.getByText('Base monthly salary can have at most 2 decimals.')).toBeInTheDocument();

    await user.type(salary, '{Backspace}');
    expect(screen.queryByText('Base monthly salary can have at most 2 decimals.')).not.toBeInTheDocument();
  });

  it('validates everything on submit, does not call the server, and focuses the first invalid field', async () => {
    const { user, onSubmit } = renderForm();

    await user.click(screen.getByRole('button', { name: 'Add employee' }));

    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText('Full name is required.')).toBeInTheDocument();
    expect(screen.getByText('Email is required.')).toBeInTheDocument();
    expect(screen.getByText('Hire date is required.')).toBeInTheDocument();
    expect(screen.getByLabelText(/full name/i)).toHaveFocus();
  });

  it('submits a trimmed EmployeeInput with allowance rows', async () => {
    const { user, onSubmit } = renderForm();
    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: '+ Add allowance' }));
    await user.type(screen.getByLabelText('Allowance 1 name'), ' Housing ');
    await user.type(screen.getByLabelText('Allowance 1 amount'), '250.50');

    await user.click(screen.getByRole('button', { name: 'Add employee' }));

    expect(onSubmit).toHaveBeenCalledWith({
      fullName: 'Ada Lovelace',
      email: 'ada@example.com',
      jobTitle: 'Engineer',
      department: 'R&D',
      hireDate: '2020-01-15',
      baseMonthlySalary: 5000,
      status: 'Active',
      allowances: [{ name: 'Housing', amount: 250.5 }],
      deductions: [],
    });
  });

  it('maps a 409 duplicate email onto the email field', async () => {
    const onSubmit = vi.fn().mockRejectedValue(
      new ApiError({ status: 409, title: 'Conflict', fieldErrors: { email: ['Email is already used by another employee.'] } }),
    );
    const { user } = renderForm(onSubmit);
    await fillValidForm(user);

    await user.click(screen.getByRole('button', { name: 'Add employee' }));

    const email = await screen.findByLabelText(/^email/i);
    await waitFor(() => expect(email).toHaveAccessibleDescription('Email is already used by another employee.'));
    expect(email).toHaveFocus();
  });

  it('maps nested 400 paths onto item rows and puts unknown keys in the top alert', async () => {
    const onSubmit = vi.fn().mockRejectedValue(
      new ApiError({
        status: 400,
        title: 'Validation failed',
        fieldErrors: {
          'allowances[0].amount': ['Amount must be at most 1,000,000.'],
          managerId: ['Manager does not exist.'],
        },
      }),
    );
    const { user } = renderForm(onSubmit);
    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: '+ Add allowance' }));
    await user.type(screen.getByLabelText('Allowance 1 name'), 'Bonus');
    await user.type(screen.getByLabelText('Allowance 1 amount'), '999999');

    await user.click(screen.getByRole('button', { name: 'Add employee' }));

    expect(await screen.findByLabelText('Allowance 1 amount')).toHaveAccessibleDescription(
      'Amount must be at most 1,000,000.',
    );
    const alert = screen.getByText("The employee couldn't be saved").closest('[role="alert"]');
    expect(alert).toHaveTextContent('Manager does not exist.');
  });

  it('shows a non-validation failure in the top alert', async () => {
    const onSubmit = vi.fn().mockRejectedValue(new ApiError({ status: 0, title: "Can't reach the server" }));
    const { user } = renderForm(onSubmit);
    await fillValidForm(user);

    await user.click(screen.getByRole('button', { name: 'Add employee' }));

    expect(await screen.findByText("Can't reach the server")).toBeInTheDocument();
  });

  it('disables the submit button while saving', async () => {
    let finish: () => void = () => {};
    const onSubmit = vi.fn(() => new Promise<void>((resolve) => (finish = resolve)));
    const { user } = renderForm(onSubmit);
    await fillValidForm(user);

    await user.click(screen.getByRole('button', { name: 'Add employee' }));

    expect(screen.getByRole('button', { name: 'Saving…' })).toBeDisabled();
    expect(screen.getByLabelText(/full name/i)).toBeDisabled();
    finish();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Add employee' })).toBeEnabled());
  });

  it('removing a row keeps later row errors on the right inputs', async () => {
    const { user } = renderForm();
    await user.click(screen.getByRole('button', { name: '+ Add deduction' }));
    await user.click(screen.getByRole('button', { name: '+ Add deduction' }));
    await user.type(screen.getByLabelText('Deduction 1 name'), 'Loan');
    await user.click(screen.getByLabelText('Deduction 2 name'));
    await user.tab();
    expect(screen.getByLabelText('Deduction 2 name')).toHaveAccessibleDescription('Name is required.');

    await user.click(screen.getByRole('button', { name: 'Remove deduction 1' }));

    expect(screen.getByLabelText('Deduction 1 name')).toHaveAccessibleDescription('Name is required.');
    expect(screen.queryByLabelText('Deduction 2 name')).not.toBeInTheDocument();
  });
});
