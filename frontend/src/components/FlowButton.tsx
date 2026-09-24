import type { ButtonHTMLAttributes } from 'react'
import { BarsSpinner } from './BarsSpinner'
import styles from './FlowButton.module.css'

interface FlowButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  isLoading?: boolean
  loadingLabel?: string
}

export function FlowButton({
  isLoading = false,
  loadingLabel,
  disabled,
  children,
  className,
  ...rest
}: FlowButtonProps) {
  return (
    <button
      type="button"
      className={[styles.button, isLoading ? styles.inFlight : '', className]
        .filter(Boolean)
        .join(' ')}
      disabled={disabled || isLoading}
      aria-busy={isLoading || undefined}
      {...rest}
    >
      <span className={styles.border} aria-hidden="true" />
      <span className={styles.content}>
        {isLoading ? <BarsSpinner label={loadingLabel ?? 'Posting'} /> : children}
      </span>
    </button>
  )
}
