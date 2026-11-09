import type { ComponentPropsWithRef } from 'react';

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost';

/** Class names for a button look, also used on router <Link>s that should look like buttons. */
export function buttonClass(variant: ButtonVariant = 'primary', small = false): string {
  return `button button--${variant}${small ? ' button--small' : ''}`;
}

interface ButtonProps extends ComponentPropsWithRef<'button'> {
  variant?: ButtonVariant;
  small?: boolean;
}

export function Button({ variant = 'primary', small = false, type = 'button', className, ...rest }: ButtonProps) {
  const classes = className ? `${buttonClass(variant, small)} ${className}` : buttonClass(variant, small);
  return <button type={type} className={classes} {...rest} />;
}
