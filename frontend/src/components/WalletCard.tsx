import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Badge } from './Badge'
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
  const shortId = `${wallet.id.slice(0, 8)}…`
  const hasActivity = wallet.entryCount > 0

  return (
    <div className={styles.card}>
      <div className={styles.identityRow}>
        {/* Spell Badge (docs/design/spell/ADAPTATIONS.md) for currency identity -
            a routine fact, not a money-direction signal, so it stays "neutral"
            per Brex's single-accent discipline: the accent is reserved for the
            Transfer action below, not spent here on a label. */}
        <Badge variant="neutral" className={styles.currencyBadge}>
          {wallet.currency}
        </Badge>
        <span className={styles.name}>{name}</span>
        <div className={styles.idGroup}>
          <span className={styles.id + ' mono'}>{shortId}</span>
          <CopyButton value={wallet.id} label="wallet ID" />
        </div>
      </div>

      <div className={styles.balanceHero}>
        <span key={wallet.balanceMinorUnits} className={styles.balanceValue + ' mono'}>
          {formatAmount(wallet.balanceMinorUnits, wallet.currency)}
        </span>
        <span className={styles.balanceCaption}>Derived from posted ledger entries</span>
      </div>

      <div className={styles.statRow}>
        <div className={styles.stat}>
          <span className={styles.statValue}>{wallet.entryCount}</span>
          <span className={styles.statLabel}>{wallet.entryCount === 1 ? 'Entry' : 'Entries'}</span>
        </div>
        <div className={styles.statDivider} aria-hidden="true" />
        <div className={styles.stat}>
          <span className={styles.statValue}>
            {hasActivity && wallet.lastActivityAtUtc
              ? new Date(wallet.lastActivityAtUtc).toLocaleDateString()
              : '—'}
          </span>
          <span className={styles.statLabel}>Last activity</span>
        </div>
      </div>

      <div className={styles.actions}>
        {!isFunding && (
          <button type="button" className={styles.actionItem} onClick={onToggleFund}>
            + Deposit
          </button>
        )}
        <Link to={`/transfer?wallet=${wallet.id}`} className={styles.actionItemPrimary}>
          Transfer →
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
