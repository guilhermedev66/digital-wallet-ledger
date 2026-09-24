import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { apiClient, isApiError, type ReconciliationReport } from '../../api'
import { BarsSpinner } from '../../components/BarsSpinner'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import tableStyles from '../../components/Table.module.css'
import { useWallets } from '../../hooks/useWallets'
import { formatAmount } from '../../lib/money'
import styles from './ReconciliationPage.module.css'

type Status = 'loading' | 'success' | 'error'

export function ReconciliationPage() {
  const { wallets, status: walletsStatus } = useWallets()
  const [searchParams, setSearchParams] = useSearchParams()
  const selectedWalletId = searchParams.get('wallet') ?? ''

  const [report, setReport] = useState<ReconciliationReport | null>(null)
  const [status, setStatus] = useState<Status>('loading')
  const [error, setError] = useState<string | null>(null)

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

  return (
    <div>
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

      {selectedWalletId && (
        <div className={styles.panel}>
          {status === 'loading' && (
            <div className={styles.loadingBlock}>
              <BarsSpinner label="Recomputing ledger balances from raw entries…" />
            </div>
          )}

          {status === 'error' && (
            <div style={{ padding: 'var(--space-5)', display: 'grid', gap: 'var(--space-3)' }}>
              <ErrorBanner message={error ?? 'Could not load reconciliation.'} />
              <Button onClick={() => load(selectedWalletId)}>Retry</Button>
            </div>
          )}

          {status === 'success' && report && (
            <div className={styles.content}>
              <div className={report.isClean ? styles.healthBanner : styles.healthBannerDrift}>
                <span className={styles.healthIcon} aria-hidden="true">
                  {report.isClean ? '✓' : '⚠'}
                </span>
                <div className={styles.healthCopy}>
                  <span className={styles.healthHeadline}>
                    {report.isClean
                      ? 'System balanced: zero drift verified'
                      : `Drift detected: ${report.unbalancedTransactions.length} unbalanced ${report.unbalancedTransactions.length === 1 ? 'transaction' : 'transactions'}`}
                  </span>
                  <span className={styles.healthSubline}>
                    {report.isClean
                      ? 'Every account balance recomputed from raw journal entries matches the runtime projected balance. Zero discrepancies found.'
                      : 'One or more transactions have unequal debits and credits, or a projected balance has drifted from its recomputed value. See the tables below.'}
                  </span>
                </div>
                <span className={styles.generatedAt + ' mono'}>
                  Generated {new Date(report.generatedAtUtc).toLocaleString()}
                </span>
              </div>

              <div className={styles.summaryStrip}>
                <div className={styles.summaryStat}>
                  <span className={styles.summaryValue + ' mono'}>
                    {report.accounts.filter((a) => a.isBalanced).length}/{report.accounts.length}
                  </span>
                  <span className={styles.summaryLabel}>Accounts balanced</span>
                </div>
                <div className={styles.summaryStat}>
                  <span
                    className={
                      styles.summaryValue +
                      ' mono ' +
                      (report.unbalancedTransactions.length > 0 ? styles.drift : '')
                    }
                  >
                    {report.unbalancedTransactions.length}
                  </span>
                  <span className={styles.summaryLabel}>Unbalanced transactions</span>
                </div>
                <p className={styles.summaryExplainer}>
                  Each account's recomputed balance is derived independently from raw ledger
                  entries — the same computation the rest of the app uses, run again from
                  scratch — so this is proof of consistency, not a second opinion trusting the
                  first.
                </p>
              </div>

              <h2 className={styles.matrixTitle}>Forensic comparison matrix</h2>
              <table className={tableStyles.table}>
                <thead>
                  <tr>
                    <th>Account</th>
                    <th>Currency</th>
                    <th>Projected balance</th>
                    <th>Recomputed balance</th>
                    <th>Drift</th>
                  </tr>
                </thead>
                <tbody>
                  {report.accounts.map((account) => (
                    <tr key={account.accountId}>
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
                      <td
                        data-label="Drift"
                        className={
                          tableStyles.numeric +
                          ' mono ' +
                          (account.isBalanced ? styles.driftOk : styles.drift)
                        }
                      >
                        {!account.isBalanced && <span aria-hidden="true">⚠ </span>}
                        {formatAmount(account.driftMinorUnits, account.currency)}
                        <span className="visually-hidden">
                          {account.isBalanced ? ' (balanced)' : ' (drift detected)'}
                        </span>
                      </td>
                    </tr>
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
                        <th>Total debits</th>
                        <th>Total credits</th>
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
          )}
        </div>
      )}
    </div>
  )
}
