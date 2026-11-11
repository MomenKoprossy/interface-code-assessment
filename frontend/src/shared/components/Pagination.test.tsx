import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { Pagination } from './Pagination';

describe('Pagination', () => {
  it('keeps focus on Next when it reaches the last page, and ignores further clicks', async () => {
    const onPageChange = vi.fn();
    function Harness() {
      const [page, setPage] = useState(2);
      return (
        <Pagination
          page={page}
          totalPages={3}
          onPageChange={(next) => {
            onPageChange(next);
            setPage(next);
          }}
        />
      );
    }
    const user = userEvent.setup();
    render(<Harness />);
    const next = screen.getByRole('button', { name: 'Next' });

    await user.click(next);
    expect(screen.getByText('Page 3 of 3')).toBeInTheDocument();
    expect(next).toHaveAttribute('aria-disabled', 'true');
    expect(next).toHaveFocus();

    await user.click(next);
    expect(onPageChange).toHaveBeenCalledTimes(1);
  });

  it('marks Previous as unavailable on the first page', async () => {
    const onPageChange = vi.fn();
    const user = userEvent.setup();
    render(<Pagination page={1} totalPages={3} onPageChange={onPageChange} />);
    const previous = screen.getByRole('button', { name: 'Previous' });

    expect(previous).toHaveAttribute('aria-disabled', 'true');
    await user.click(previous);
    expect(onPageChange).not.toHaveBeenCalled();
  });
});
