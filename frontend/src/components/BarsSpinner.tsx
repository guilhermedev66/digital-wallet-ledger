import styles from './BarsSpinner.module.css'

export function BarsSpinner({ label }: { label?: string }) {
  return (
    <span className={styles.wrap} role="status" aria-label={label ?? 'Loading'}>
      <span className={styles.bars} aria-hidden="true">
        <span className={styles.bar} />
        <span className={styles.bar} />
        <span className={styles.bar} />
        <span className={styles.bar} />
        <span className={styles.bar} />
      </span>
      {label && <span className={styles.label}>{label}</span>}
    </span>
  )
}
