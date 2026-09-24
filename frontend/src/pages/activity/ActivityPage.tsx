import { useCallback, useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { apiClient, isApiError, type LedgerEntry, type Transaction } from '../../api'
import { Badge } from '../../components/Badge'
import { Button } from '../../components/Button'
import { CopyButton } from '../../components/CopyButton'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import { SkeletonRows } from '../../components/Skeleton'
import { useWallets } from '../../hooks/useWallets'
import { formatAmount } from '../../lib/money'
import styles from './ActivityPage.module.css'

const PAGE_SIZE = 20

type Status = 'loading' | 'success' | 'error'
type ReversePhase = 'idle' | 'confirming' | 'submitting'

function leg(tx: Transaction, direction: LedgerEntry['direction']): LedgerEntry | undefined {
  return tx.entries.find((e) => e.direction === direction)
}

function shortId(id: string): string {
  return id.length > 14 ? `${id.slice(0, 6)}…${id.slice(-4)}` : id
}

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
        <div>
          <h1 className={styles.title}>Activity</h1>
          <p className={styles.subtitle}>
            The journal, not a bank statement — every entry shows both legs. This wallet's own
            leg is highlighted; the counterparty leg is connected beneath it, never hidden.
          </p>
        </div>
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
                  {w.displayName ?? 'Untitled wallet'} · {w.currency}
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
              <div className={styles.journalHead} aria-hidden="true">
                <span>Entry</span>
                <span>Own leg</span>
                <span>Counterparty leg</span>
                <span>Actions</span>
              </div>
              <ol className={styles.journal}>
                {items.map((tx) => {
                  const debitEntry = leg(tx, 'Debit')
                  const creditEntry = leg(tx, 'Credit')
                  const debitIsOwn = debitEntry?.accountId === selectedWallet.id
                  const ownEntry = debitIsOwn ? debitEntry : creditEntry
                  const counterpartyEntry = debitIsOwn ? creditEntry : debitEntry
                  const ownDirection: 'Debit' | 'Credit' = debitIsOwn ? 'Debit' : 'Credit'
                  const counterpartyDirection: 'Debit' | 'Credit' = debitIsOwn
                    ? 'Credit'
                    : 'Debit'
                  const isReversed = reversedTransactionIds.has(tx.id)
                  const canReverse = tx.type !== 'Reversal' && !isReversed
                  const isConfirming = reversingId === tx.id

                  return (
                    <li key={tx.id} className={styles.entry}>
                      <div className={styles.entryHead}>
                        <div className={styles.entryMeta}>
                          <time className={styles.entryDate + ' mono'} dateTime={tx.postedAtUtc}>
                            {new Date(tx.postedAtUtc).toLocaleString()}
                          </time>
                          <Badge variant="neutral">{tx.type}</Badge>
                          {isReversed && <Badge variant="danger">Reversed</Badge>}
                        </div>
                        <div className={styles.entryId}>
                          <span className="mono">{shortId(tx.id)}</span>
                          <CopyButton value={tx.id} label="transaction ID" />
                        </div>
                      </div>

                      {tx.type === 'Reversal' && tx.reversalOfTransactionId && (
                        <p className={styles.reversalNote}>
                          Reverses{' '}
                          <span className="mono">{shortId(tx.reversalOfTransactionId)}</span>
                          <CopyButton
                            value={tx.reversalOfTransactionId}
                            label="reversed transaction ID"
                          />
                        </p>
                      )}

                      <div className={styles.legs}>
                        <div className={styles.leg + ' ' + styles.legOwn}>
                          <Badge variant="neutral">
                            {ownDirection === 'Debit' ? '↗ Debit' : '↙ Credit'}
                          </Badge>
                          <span className={styles.legAccount}>This wallet</span>
                          <span
                            className={
                              'amount ' + (ownDirection === 'Credit' ? styles.positive : '')
                            }
                          >
                            {ownEntry
                              ? `${ownDirection === 'Debit' ? '+' : '-'}${formatAmount(
                                  ownEntry.amountMinorUnits,
                                  ownEntry.currency,
                                )}`
                              : '—'}
                          </span>
                        </div>
                        <div className={styles.leg + ' ' + styles.legCounterparty}>
                          <span className={styles.treeBracket} aria-hidden="true">
                            └──
                          </span>
                          <Badge variant="neutral">
                            {counterpartyDirection === 'Debit' ? '↗ Debit' : '↙ Credit'}
                          </Badge>
                          <span className={styles.legAccount + ' mono'}>
                            {counterpartyEntry?.accountId ?? '—'}
                          </span>
                          <span
                            className={
                              'amount ' +
                              (counterpartyDirection === 'Credit' ? styles.positive : '')
                            }
                          >
                            {counterpartyEntry
                              ? `${counterpartyDirection === 'Debit' ? '+' : '-'}${formatAmount(
                                  counterpartyEntry.amountMinorUnits,
                                  counterpartyEntry.currency,
                                )}`
                              : '—'}
                          </span>
                        </div>
                      </div>

                      <div className={styles.entryActions}>
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
                      </div>
                    </li>
                  )
                })}
              </ol>
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
