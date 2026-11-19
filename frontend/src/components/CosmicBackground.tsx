import styles from './CosmicBackground.module.css'

/** Fixed, decorative SafePal-style backdrop: slow violet orbs + faint stars. Transform-only animation. */
export function CosmicBackground() {
  return (
    <div className={styles.root} aria-hidden="true">
      <div className={styles.orbs}>
        <span className={[styles.orb, styles.orbA].join(' ')} />
        <span className={[styles.orb, styles.orbB].join(' ')} />
        <span className={[styles.orb, styles.orbC].join(' ')} />
      </div>
      <div className={styles.stars} />
      <div className={[styles.stars, styles.starsFar].join(' ')} />
    </div>
  )
}
