import styles from './ErrorBanner.module.css'

export function ErrorBanner({ message }: { message: string }) {
  return (
    <div className={styles.banner} role="alert">
      <span className={styles.icon} aria-hidden="true">
        &#9888;
      </span>
      <span>{message}</span>
    </div>
  )
}
