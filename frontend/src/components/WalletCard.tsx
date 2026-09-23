import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import buttonStyles from './Button.module.css'
import { Button } from './Button'
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
      <div className={styles.identity}>
        <div className={styles.nameRow}>
          <span className={styles.name}>{name}</span>
          <span className={styles.currency}>{wallet.currency}</span>
        </div>
        <div className={styles.idRow}>
          <span className={styles.id + ' mono'}>{wallet.id}</span>
          <CopyButton value={wallet.id} label="wallet ID" />
        </div>
      </div>

      <span key={wallet.balanceMinorUnits} className={styles.balance + ' amount'}>
        {formatAmount(wallet.balanceMinorUnits, wallet.currency)}
      </span>

      <span className={styles.telemetry}>{telemetry}</span>

      <div className={styles.actions}>
        {!isFunding && <Button onClick={onToggleFund}>+ Deposit</Button>}
        <Link
          to={`/transfer?wallet=${wallet.id}`}
          className={[buttonStyles.button, buttonStyles.secondary].join(' ')}
        >
          → Transfer
        </Link>
        <Link
          to={`/activity?wallet=${wallet.id}`}
          className={[buttonStyles.button, buttonStyles.secondary].join(' ')}
        >
          Activity
        </Link>
      </div>

      {fundingSlot}
    </div>
  )
}
