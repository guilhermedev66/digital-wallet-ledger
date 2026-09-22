import type { FormEvent, ReactNode } from 'react'
import styles from './AuthLayout.module.css'

export function AuthLayout({
  title,
  children,
  footer,
  onSubmit,
}: {
  title: string
  children: ReactNode
  footer?: ReactNode
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}) {
  return (
    <div className={styles.page}>
      <div className={styles.panel}>
        <div className={styles.brand}>
          <span className={styles.brandName}>Wallet &amp; Ledger</span>
          <span className={styles.tagline}>
            Simulated double-entry ledger. No real money, no real banks.
          </span>
        </div>
        <form className={styles.form} onSubmit={onSubmit} noValidate>
          <h1 className="visually-hidden">{title}</h1>
          {children}
        </form>
        {footer && <div className={styles.footer}>{footer}</div>}
      </div>
    </div>
  )
}
