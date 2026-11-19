import styles from './Brand.module.css'

/** Original pixel-art wallet/coin mark (public/favicon.svg) + "Wallet & Ledger" wordmark. */
export function Brand({ size = 28, showText = true }: { size?: number; showText?: boolean }) {
  return (
    <span className={styles.brand}>
      <img src="/favicon.svg" width={size} height={size} alt="" className={styles.mark} />
      {showText && (
        <span className={styles.word}>
          Wallet <span className={styles.amp}>&amp;</span> Ledger
        </span>
      )}
      {!showText && <span className="visually-hidden">Wallet &amp; Ledger</span>}
    </span>
  )
}
