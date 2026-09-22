import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { apiClient, isApiError, type Transaction } from '../../api'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import { SkeletonRows } from '../../components/Skeleton'
import tableStyles from '../../components/Table.module.css'
import { useWallets } from '../../hooks/useWallets'
import { formatSignedAmount } from '../../lib/money'
import styles from './ActivityPage.module.css'

const PAGE_SIZE = 20

type Status = 'loading' | 'success' | 'error'
type ReversePhase = 'idle' | 'confirming' | 'submitting'

export function ActivityPage() {
  const { wallets, status: walletsStatus } = useWallets()
  const [searchParams, setSearchParams] = useSearchParams()
  const selectedWalletId = searchParams.get('wallet') ?? ''

  const [items, setItems] = useState<Transaction[]>([])
  const [page, setPage] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [status, setStatus] = useState<Status>('loading')
  const [error, setError] = useState<string | null>(null)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [reversingId, setReversingId] = useState<string | null>(null)
  const [reversePhase, setReversePhase] = useState<ReversePhase>('idle')
  const [reverseError, setReverseError] = useState<string | null>(null)

  useEffect(() => {
    if (!selectedWalletId && wallets.length > 0) {
      setSearchParams({ wallet: wallets[0].id }, { replace: true })
    }
  }, [selectedWalletId, wallets, setSearchParams])

  const load = useCallback(async (walletId: string) => {
    setStatus('loading')
    setError(null)
    try {
      const result = await apiClient.getHistory(walletId, 1, PAGE_SIZE)
      setItems(result.items)
      setPage(result.page)
      setTotalCount(result.totalCount)
      setStatus('success')
    } catch (err) {
      setError(isApiError(err) ? err.message : 'Could not load activity.')
      setStatus('error')
    }
  }, [])

  useEffect(() => {
    if (selectedWalletId) load(selectedWalletId)
  }, [selectedWalletId, load])

  const hasMore = items.length < totalCount

  async function loadMore() {
    if (!hasMore || !selectedWalletId) return
    setIsLoadingMore(true)
    try {
      const result = await apiClient.getHistory(selectedWalletId, page + 1, PAGE_SIZE)
      setItems((prev) => [...prev, ...result.items])
      setPage(result.page)
      setTotalCount(result.totalCount)
    } catch (err) {
      setError(isApiError(err) ? err.message : 'Could not load more activity.')
    } finally {
      setIsLoadingMore(false)
    }
  }

  const selectedWallet = wallets.find((w) => w.id === selectedWalletId)

  // Best-effort, current-page-only: hides the Reverse action for a transaction that's
  // already been reversed, without needing a dedicated backend lookup. If the reversal
  // happened on a page not currently loaded, the button still shows - the backend's own
  // 409 (TransactionAlreadyReversedException) is the real guard either way.
  const reversedTransactionIds = new Set(
    items.map((t) => t.reversalOfTransactionId).filter((id): id is string => id !== null),
  )

  async function confirmReverse(transactionId: string) {
    if (!selectedWalletId) return
    setReversePhase('submitting')
    setReverseError(null)
    try {
      await apiClient.reverseTransaction(selectedWalletId, transactionId, crypto.randomUUID())
      setReversingId(null)
      setReversePhase('idle')
      await load(selectedWalletId)
    } catch (err) {
      setReverseError(isApiError(err) ? err.message : 'Could not reverse this transaction.')
      setReversePhase('confirming')
    }
  }

  return (
    <div>
      <div className={styles.header}>
        <h1 className={styles.title}>Activity</h1>
        {walletsStatus === 'success' && wallets.length > 0 && (
          <div>
            <label className="visually-hidden" htmlFor="activity-wallet">
              Wallet
            </label>
            <select
              id="activity-wallet"
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
          description="Create a wallet on the dashboard to see its activity here."
        />
      )}

      {selectedWalletId && (
        <div className={styles.panel}>
          {status === 'loading' && (
            <div style={{ padding: 'var(--space-5)' }}>
              <SkeletonRows count={5} />
            </div>
          )}

          {status === 'error' && (
            <div style={{ padding: 'var(--space-5)', display: 'grid', gap: 'var(--space-3)' }}>
              <ErrorBanner message={error ?? 'Could not load activity.'} />
              <Button onClick={() => load(selectedWalletId)}>Retry</Button>
            </div>
          )}

          {status === 'success' && items.length === 0 && (
            <div style={{ padding: 'var(--space-5)' }}>
              <EmptyState
                title="No activity yet"
                description="Fund this wallet or make a transfer to see entries here."
              />
            </div>
          )}

          {status === 'success' && items.length > 0 && selectedWallet && (
            <>
              <table className={tableStyles.table}>
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Type</th>
                    <th>Amount</th>
                    <th aria-label="Actions" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((tx) => {
                    const entry = tx.entries.find((e) => e.accountId === selectedWallet.id)
                    // Debit-normal: a Debit entry increases this wallet's balance (money in),
                    // Credit decreases it (money out) - see MEMORY.md / API_CONTRACT.md.
                    const sign = entry?.direction === 'Debit' ? 1 : -1
                    const canReverse = tx.type !== 'Reversal' && !reversedTransactionIds.has(tx.id)
                    const isConfirming = reversingId === tx.id
                    return (
                      <tr key={tx.id}>
                        <td data-label="Date">{new Date(tx.postedAtUtc).toLocaleString()}</td>
                        <td data-label="Type">{tx.type}</td>
                        <td
                          data-label="Amount"
                          className={
                            tableStyles.numeric +
                            ' amount ' +
                            (sign > 0 ? styles.credit : styles.debit)
                          }
                        >
                          {entry
                            ? formatSignedAmount(entry.amountMinorUnits, entry.currency, sign)
                            : '—'}
                        </td>
                        <td data-label="Actions">
                          {canReverse && !isConfirming && (
                            <Button
                              onClick={() => {
                                setReversingId(tx.id)
                                setReversePhase('confirming')
                                setReverseError(null)
                              }}
                            >
                              Reverse
                            </Button>
                          )}
                          {isConfirming && (
                            <div className={styles.reverseConfirm}>
                              {reverseError && <ErrorBanner message={reverseError} />}
                              <span>Reverse this transaction?</span>
                              <Button
                                variant="primary"
                                isLoading={reversePhase === 'submitting'}
                                onClick={() => confirmReverse(tx.id)}
                              >
                                Confirm
                              </Button>
                              <Button
                                disabled={reversePhase === 'submitting'}
                                onClick={() => {
                                  setReversingId(null)
                                  setReversePhase('idle')
                                  setReverseError(null)
                                }}
                              >
                                Cancel
                              </Button>
                            </div>
                          )}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
              {hasMore && (
                <div className={styles.footer}>
                  <Button onClick={loadMore} isLoading={isLoadingMore}>
                    Load more
                  </Button>
                </div>
              )}
            </>
          )}
        </div>
      )}
    </div>
  )
}
