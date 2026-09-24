import styles from './Kbd.module.css'

export function Kbd({ keys }: { keys: string[] }) {
  return (
    <span className={styles.group} aria-hidden="true">
      {keys.map((key, i) => (
        <kbd key={i} className={styles.key}>
          {key}
        </kbd>
      ))}
    </span>
  )
}
