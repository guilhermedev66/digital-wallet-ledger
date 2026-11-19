import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { motion, useReducedMotion } from 'motion/react'
import { Activity, CheckCircle2, Scale, Sigma } from 'lucide-react'
import { ShimmerText } from '../../components/shimmer-text'
import { apiClient, isApiError, type ReconciliationReport } from '../../api'
import { Badge } from '../../components/Badge'
import { LedgerScale } from '../../components/LedgerScale'
import { SkeletonScore, SkeletonTable } from '../../components/Skeleton'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import tableStyles from '../../components/Table.module.css'
import { useWallets } from '../../hooks/useWallets'
import type { AccountReconciliation, Currency, ReconciliationReport as Report } from '../../api'
import { formatAmount } from '../../lib/money'
import styles from './ReconciliationPage.module.css'

type Status = 'loading' | 'success' | 'error'

interface Score {
  label: string
  value: string
  driftText: string
  hasDrift: boolean
}

// Drift is only summed within one currency - never across currencies. With several
// currencies in the report, the headline falls back to a count of drifting accounts.
interface ScaleModel {
  tilt: number
  leftValue?: string
  rightValue?: string
  caption: string
  ariaLabel: string
}

// Real report data only. Left pan = Σ debits, right = Σ credits (of the unbalanced transactions the
// report lists); sums are shown only when they share one currency. With drift but no unbalanced
// transaction, the scale tips by the sign of the summed drift (single currency) and says so.
function describeScale(report: Report): ScaleModel {
  const un = report.unbalancedTransactions
  const currencies = new Set<Currency>(un.map((t) => t.currency))
  if (un.length > 0) {
    const debit = un.reduce((n, t) => n + t.totalDebitMinorUnits, 0)
    const credit = un.reduce((n, t) => n + t.totalCreditMinorUnits, 0)
    const single = currencies.size === 1
    const diff = credit - debit
    const max = Math.max(debit, credit, 1)
    const sign = single ? (diff === 0 ? 1 : Math.sign(diff)) : 1
    const tilt = sign * (single ? Math.min(14, 6 + (8 * Math.abs(diff)) / max) : 10)
    const cur = [...currencies][0]
    return {
      tilt,
      leftValue: single ? formatAmount(debit, cur) : undefined,
      rightValue: single ? formatAmount(credit, cur) : undefined,
      caption: `${un.length} unbalanced ${un.length === 1 ? 'transaction' : 'transactions'}`,
      ariaLabel: 'Scale tipped: some transactions have unequal debits and credits',
    }
  }
  const drifting = report.accounts.filter((a) => !a.isBalanced)
  if (drifting.length > 0) {
    const accountCurrencies = new Set<Currency>(report.accounts.map((a) => a.currency))
    const drift = drifting.reduce((n, a) => n + a.driftMinorUnits, 0)
    const sign = accountCurrencies.size === 1 && drift < 0 ? 1 : -1
    return {
      tilt: sign * 10,
      caption: `Drift detected on ${drifting.length} ${drifting.length === 1 ? 'account' : 'accounts'}`,
      ariaLabel: 'Scale tipped: a projected balance differs from its recomputed value',
    }
  }
  return {
    tilt: 0,
    caption: 'Σ Debits = Σ Credits · no unbalanced transactions',
    ariaLabel: 'Scale level: debits equal credits',
  }
}

function describeScore(accounts: AccountReconciliation[], isClean: boolean): Score {
  const drifting = accounts.filter((a) => !a.isBalanced)
  const currencies = new Set<Currency>(accounts.map((a) => a.currency))
  const hasDrift = drifting.length > 0
  let driftText = '0'
  if (hasDrift) {
    driftText =
      currencies.size === 1
        ? formatAmount(
            Math.abs(drifting.reduce((sum, a) => sum + a.driftMinorUnits, 0)),
            accounts[0].currency,
          )
        : `${drifting.length} accounts`
  } else if (currencies.size === 1 && accounts.length > 0) {
    driftText = formatAmount(0, accounts[0].currency)
  }
  if (isClean && !hasDrift) return { label: 'BALANCED', value: '0', driftText, hasDrift }
  if (hasDrift && currencies.size === 1) {
    return { label: 'DRIFT', value: driftText, driftText, hasDrift }
  }
  return { label: 'NEEDS ATTENTION', value: String(drifting.length || 1), driftText, hasDrift }
}

export function ReconciliationPage() {
  const { wallets, status: walletsStatus } = useWallets()
  const [searchParams, setSearchParams] = useSearchParams()
  const selectedWalletId = searchParams.get('wallet') ?? ''

  const [report, setReport] = useState<ReconciliationReport | null>(null)
  const [status, setStatus] = useState<Status>('loading')
  const [error, setError] = useState<string | null>(null)
  const reduce = useReducedMotion()

  useEffect(() => {
    if (!selectedWalletId && wallets.length > 0) {
      setSearchParams({ wallet: wallets[0].id }, { replace: true })
    }
  }, [selectedWalletId, wallets, setSearchParams])

  const load = useCallback(async (walletId: string) => {
    setStatus('loading')
    setError(null)
    try {
      const result = await apiClient.getWalletReconciliation(walletId)
      setReport(result)
      setStatus('success')
    } catch (err) {
      setError(isApiError(err) ? err.message : 'Could not load reconciliation.')
      setStatus('error')
    }
  }, [])

  useEffect(() => {
    if (selectedWalletId) load(selectedWalletId)
  }, [selectedWalletId, load])

  const totalAccounts = report?.accounts.length ?? 0
  const balancedAccounts = report?.accounts.filter((a) => a.isBalanced).length ?? 0
  const unbalancedCount = report?.unbalancedTransactions.length ?? 0
  const unbalancedWord = unbalancedCount === 1 ? 'transaction' : 'transactions'
  const driftingAccounts = totalAccounts - balancedAccounts
  const score = report ? describeScore(report.accounts, report.isClean) : null
  const scale = report ? describeScale(report) : null

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <div>
          <h1 className={styles.title}>Reconciliation</h1>
          <p className={styles.subtitle}>
            The balance every endpoint uses, recomputed independently from raw ledger entries -
            proof the two never silently drift apart.
          </p>
        </div>
        {walletsStatus === 'success' && wallets.length > 0 && (
          <div>
            <label className="visually-hidden" htmlFor="reconciliation-wallet">
              Wallet
            </label>
            <select
              id="reconciliation-wallet"
              className={styles.select}
              value={selectedWalletId}
              onChange={(e) => setSearchParams({ wallet: e.target.value })}
            >
              {wallets.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.displayName ?? 'Untitled wallet'}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {walletsStatus === 'success' && wallets.length === 0 && (
        <EmptyState
          title="No wallets yet"
          description="Create a wallet on the dashboard to reconcile it here."
        />
      )}

      {(selectedWalletId ? status === 'loading' : walletsStatus === 'loading') && (
        <div className={styles.panel}>
          <div className={styles.loadingBlock}>
            <SkeletonScore />
            <SkeletonTable rows={2} cols={5} label="Loading comparison table" />
          </div>
        </div>
      )}

      {selectedWalletId && status === 'error' && (
        <div className={styles.panel}>
          <div className={styles.stack}>
            <ErrorBanner message={error ?? 'Could not load reconciliation.'} />
            <Button onClick={() => load(selectedWalletId)}>Retry</Button>
          </div>
        </div>
      )}

      {selectedWalletId && status === 'success' && report && score && scale && (
        <>
          <section className={['glass', styles.scaleCard].join(' ')} aria-label="Debit and credit balance">
            <LedgerScale
              key={report.generatedAtUtc}
              tilt={scale.tilt}
              leftValue={scale.leftValue}
              rightValue={scale.rightValue}
              caption={scale.caption}
              ariaLabel={scale.ariaLabel}
            />
          </section>
          <section
            className={[styles.scoreCard, report.isClean ? '' : styles.scoreDrift].join(' ')}
            aria-label="Reconciliation scoreboard"
          >
            <div className={styles.scoreLeft}>
              {score.label === 'BALANCED' && !reduce ? (
                <ShimmerText
                  className="text-xs font-bold tracking-[0.12em] text-[var(--color-credit)] [--shimmer-contrast:rgba(255,255,255,0.9)]"
                  delay={0.4}
                >
                  BALANCED
                </ShimmerText>
              ) : (
                <span className={[styles.scoreLabel, score.label === 'BALANCED' ? styles.good : styles.bad].join(' ')}>
                  {score.label}
                </span>
              )}
              <span className={styles.scoreNumber + ' amount'}>{score.value}</span>
              <div
                className={styles.dots}
                role="img"
                aria-label={`${balancedAccounts} of ${totalAccounts} accounts verified balanced`}
              >
                {Array.from({ length: Math.max(40, Math.ceil(totalAccounts / 10) * 10) }).map(
                  (_, i) => {
                    const account = report.accounts[i]
                    const cls = !account
                      ? styles.dotEmpty
                      : account.isBalanced
                        ? styles.dotOk
                        : styles.dotBad
                    // Diagonal wave: every slot flashes in sequence; verified accounts stay lit.
                    const wave = ((i % 10) + Math.floor(i / 10)) * 0.045
                    return (
                      <motion.span
                        key={i}
                        className={[styles.dot, cls].join(' ')}
                        initial={reduce ? { opacity: 0 } : { opacity: 0, scale: 0.2 }}
                        animate={reduce ? { opacity: 1 } : { opacity: [0, 1, 1], scale: [0.2, 1.7, 1] }}
                        transition={{ duration: 0.55, delay: 0.2 + wave, ease: 'easeOut' }}
                      />
                    )
                  },
                )}
              </div>
            </div>
            <ul className={styles.metrics}>
              <motion.li
                className={styles.metric}
                initial={reduce ? { opacity: 0 } : { opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ delay: 0.15 + 0 * 0.09, duration: 0.4, ease: 'easeOut' }}
              >
                <span className={[styles.metricIcon, styles.iconMint].join(' ')} aria-hidden="true">
                  <CheckCircle2 size={18} />
                </span>
                <span className={styles.metricLabel}>Accounts verified</span>
                <span className={styles.metricValue + ' amount'}>
                  {balancedAccounts}/{totalAccounts}
                </span>
              </motion.li>
              <motion.li
                className={styles.metric}
                initial={reduce ? { opacity: 0 } : { opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ delay: 0.15 + 1 * 0.09, duration: 0.4, ease: 'easeOut' }}
              >
                <span className={[styles.metricIcon, styles.iconViolet].join(' ')} aria-hidden="true">
                  <Sigma size={18} />
                </span>
                <span className={styles.metricLabel}>Σ Debits = Σ Credits</span>
                <span
                  className={[
                    styles.metricValue,
                    unbalancedCount > 0 ? styles.bad : styles.good,
                  ].join(' ')}
                >
                  {unbalancedCount === 0 ? 'Holds' : `${unbalancedCount} unbalanced ${unbalancedWord}`}
                </span>
              </motion.li>
              <motion.li
                className={styles.metric}
                initial={reduce ? { opacity: 0 } : { opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ delay: 0.15 + 2 * 0.09, duration: 0.4, ease: 'easeOut' }}
              >
                <span className={[styles.metricIcon, styles.iconAmber].join(' ')} aria-hidden="true">
                  <Scale size={18} />
                </span>
                <span className={styles.metricLabel}>Projected = recomputed</span>
                <span
                  className={[styles.metricValue, driftingAccounts > 0 ? styles.bad : styles.good].join(' ')}
                >
                  {driftingAccounts === 0 ? 'Holds' : `${driftingAccounts} drifting`}
                </span>
              </motion.li>
              <motion.li
                className={styles.metric}
                initial={reduce ? { opacity: 0 } : { opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ delay: 0.15 + 3 * 0.09, duration: 0.4, ease: 'easeOut' }}
              >
                <span className={[styles.metricIcon, styles.iconRose].join(' ')} aria-hidden="true">
                  <Activity size={18} />
                </span>
                <span className={styles.metricLabel}>Drift</span>
                <span
                  className={[styles.metricValue, 'amount', score.hasDrift ? styles.bad : ''].join(' ')}
                >
                  {score.driftText}
                </span>
              </motion.li>
            </ul>
            <p className={styles.scoreNote}>
              {report.isClean
                ? 'Every account balance recomputed from raw journal entries matches the runtime projected balance.'
                : 'One or more transactions have unequal debits and credits, or a projected balance has drifted from its recomputed value. See below.'}{' '}
              <span className="mono">Generated {new Date(report.generatedAtUtc).toLocaleString()}</span>
            </p>
          </section>

          <div className={styles.panel}>
            <div className={styles.content}>
              <div className={styles.matrixHeader}>
                <h2 className={styles.matrixTitle}>Projected vs recomputed</h2>
                <p className={styles.matrixExplainer}>
                  Each account's recomputed balance is derived independently from raw ledger
                  entries — the same computation the rest of the app uses, run again from
                  scratch — so this table is proof of consistency, not a second opinion trusting
                  the first.
                </p>
              </div>
              <table className={tableStyles.table}>
                <thead>
                  <tr>
                    <th>Account</th>
                    <th>Currency</th>
                    <th className={tableStyles.numeric}>Projected balance</th>
                    <th className={tableStyles.numeric}>Recomputed balance</th>
                    <th className={tableStyles.numeric}>Drift</th>
                  </tr>
                </thead>
                <tbody>
                  {report.accounts.map((account, rowIndex) => (
                    <motion.tr
                      key={account.accountId}
                      initial={reduce ? { opacity: 0 } : { opacity: 0, y: 14 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ delay: 0.5 + rowIndex * 0.08, duration: 0.4, ease: 'easeOut' }}
                    >
                      <td data-label="Account">
                        <span className={styles.accountType}>{account.accountType}</span>
                        <span className={styles.accountId + ' mono'}>{account.accountId}</span>
                      </td>
                      <td data-label="Currency">{account.currency}</td>
                      <td data-label="Projected balance" className={tableStyles.numeric + ' mono'}>
                        {formatAmount(account.projectedBalanceMinorUnits, account.currency)}
                      </td>
                      <td data-label="Recomputed balance" className={tableStyles.numeric + ' mono'}>
                        {formatAmount(account.recomputedBalanceMinorUnits, account.currency)}
                      </td>
                      <td data-label="Drift" className={tableStyles.numeric + ' ' + styles.driftCell}>
                        <span className="mono">
                          {formatAmount(account.driftMinorUnits, account.currency)}
                        </span>
                        {!account.isBalanced && <Badge variant="danger">Drift</Badge>}
                        <span className="visually-hidden">
                          {account.isBalanced ? ' (balanced)' : ' (drift detected)'}
                        </span>
                      </td>
                    </motion.tr>
                  ))}
                </tbody>
              </table>

              {report.unbalancedTransactions.length > 0 && (
                <div className={styles.unbalancedSection}>
                  <h2 className={styles.unbalancedTitle}>Unbalanced transactions</h2>
                  <table className={tableStyles.table}>
                    <thead>
                      <tr>
                        <th>Transaction</th>
                        <th>Currency</th>
                        <th className={tableStyles.numeric}>Total debits</th>
                        <th className={tableStyles.numeric}>Total credits</th>
                      </tr>
                    </thead>
                    <tbody>
                      {report.unbalancedTransactions.map((t) => (
                        <tr key={`${t.transactionId}-${t.currency}`}>
                          <td data-label="Transaction" className="mono">
                            {t.transactionId}
                          </td>
                          <td data-label="Currency">{t.currency}</td>
                          <td data-label="Total debits" className={tableStyles.numeric + ' amount'}>
                            {formatAmount(t.totalDebitMinorUnits, t.currency)}
                          </td>
                          <td data-label="Total credits" className={tableStyles.numeric + ' amount'}>
                            {formatAmount(t.totalCreditMinorUnits, t.currency)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  )
}
