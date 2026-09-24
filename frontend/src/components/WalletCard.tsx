import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { CopyButton } from './CopyButton'
import type { WalletSummary } from '../hooks/useWallets'
import { formatAmount } from '../lib/money'
import styles from './WalletCard.module.css'

export function WalletCard({
  wallet,
  isFunding,
  onToggleFund,
  fundingSlot,
}: {
  wallet: WalletSummary
  isFunding: boolean
  onToggleFund: () => void
  fundingSlot?: ReactNode
}) {
  const name = wallet.displayName ?? 'Untitled wallet'
  const telemetry =
    wallet.entryCount === 0
      ? 'No activity yet'
      : `${wallet.entryCount} posted ${wallet.entryCount === 1 ? 'entry' : 'entries'}${
          wallet.lastActivityAtUtc
            ? ` · Last: ${new Date(wallet.lastActivityAtUtc).toLocaleString()}`
            : ''
        }`

  return (
    <div className={styles.card}>
      <div className={styles.statusRow}>
        <span className={styles.currency}>{wallet.currency}</span>
        <span className={styles.status}>
          <span className={styles.statusDot} aria-hidden="true" />
          NODE ACTIVE
        </span>
      </div>

      <div className={styles.identity}>
        <span className={styles.name}>{name}</span>
        <div className={styles.idRow}>
          <span className={styles.id + ' mono'}>{wallet.id}</span>
          <CopyButton value={wallet.id} label="wallet ID" />
        </div>
      </div>

      <div className={styles.balanceBlock}>
        <span className={styles.balanceLabel}>Derived balance</span>
        <span key={wallet.balanceMinorUnits} className={styles.balance + ' mono'}>
          {formatAmount(wallet.balanceMinorUnits, wallet.currency)}
        </span>
      </div>

      <span className={styles.telemetry}>{telemetry}</span>

      <div className={styles.actions}>
        {!isFunding && (
          <button type="button" className={styles.actionItem} onClick={onToggleFund}>
            + Deposit
          </button>
        )}
        <Link to={`/transfer?wallet=${wallet.id}`} className={styles.actionItem}>
          → Transfer
        </Link>
        <Link to={`/activity?wallet=${wallet.id}`} className={styles.actionItem}>
          Activity
        </Link>
      </div>

      <div className={[styles.drawer, isFunding ? styles.drawerOpen : ''].join(' ')}>
        <div className={styles.drawerInner}>{fundingSlot}</div>
      </div>
    </div>
  )
}
