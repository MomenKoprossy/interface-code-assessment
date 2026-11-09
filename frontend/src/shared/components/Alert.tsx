import type { ReactNode } from 'react';

type Tone = 'error' | 'info' | 'warning' | 'success';

const icons: Record<Tone, string> = { error: '!', info: 'i', warning: '!', success: '✓' };

interface AlertProps {
  tone?: Tone;
  title?: string;
  children?: ReactNode;
}

/** Errors interrupt (role=alert); everything else is announced politely (role=status). */
export function Alert({ tone = 'error', title, children }: AlertProps) {
  return (
    <div className={`alert alert--${tone}`} role={tone === 'error' ? 'alert' : 'status'}>
      <span className="alert__icon" aria-hidden="true">
        {icons[tone]}
      </span>
      <div className="alert__body">
        {title && <p className="alert__title">{title}</p>}
        {children}
      </div>
    </div>
  );
}
