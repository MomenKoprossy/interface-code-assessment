import { Button } from './Button';

interface PaginationProps {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, totalPages, onPageChange }: PaginationProps) {
  if (totalPages <= 1) return null;
  const isFirst = page <= 1;
  const isLast = page >= totalPages;
  // aria-disabled rather than disabled: a disabled button drops keyboard focus to <body> on the first/last page.
  return (
    <nav className="pagination" aria-label="Pagination">
      <Button variant="secondary" small aria-disabled={isFirst} onClick={() => !isFirst && onPageChange(page - 1)}>
        ← Previous
      </Button>
      <span className="pagination__status" aria-live="polite">
        Page {page} of {totalPages}
      </span>
      <Button variant="secondary" small aria-disabled={isLast} onClick={() => !isLast && onPageChange(page + 1)}>
        Next →
      </Button>
    </nav>
  );
}
