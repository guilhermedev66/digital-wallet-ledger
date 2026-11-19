import { useCallback, useEffect, useState } from 'react'
import { AnimatePresence, motion, useReducedMotion } from 'motion/react'
import { useSearchParams } from 'react-router-dom'
import { apiClient, isApiError, type Transaction } from '../../api'
import { ChevronDown } from 'lucide-react'
import { Badge } from '../../components/Badge'
import { Button } from '../../components/Button'
import { CopyButton } from '../../components/CopyButton'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import { SkeletonList } from '../../components/Skeleton'
import { TxIcon } from '../../components/TxIcon'
import { useWallets } from '../../hooks/useWallets'
import { formatAmount, formatSignedAmount } from '../../lib/money'
import { describeTx, shortId } from '../../lib/txView'
import styles from './ActivityPage.module.css'

const PAGE_SIZE = 20

type Status = 'loading' | 'success' | 'error'
type ReversePhase = 'idle' | 'confirming' | 'submitting'

type Period = 'Today' | 'This week' | 'Earlier'
const PERIODS: Period[] = ['Today', 'This week', 'Earlier']

// Buckets use the transactions' real posted dates against the local calendar day.
function periodOf(iso: string, now: Date): Period {
  const posted = new Date(iso)
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
  const dayMs = 24 * 60 * 60 * 1000
  if (posted.getTime() >= startOfToday) return 'Today'
  if (posted.getTime() >= startOfToday - 6 * dayMs) return 'This week'
  return 'Earlier'
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
  const [expandedId, setExpandedId] = useState<string | null>(null)
  // The transaction that was just reversed: its row "rewinds" before the Reversed badge pops in.
  const [rewindId, setRewindId] = useState<string | null>(null)
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
    if (selectedWalletId) {
      setItems([])
      load(selectedWalletId)
    }
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
      setRewindId(transactionId)
      await load(selectedWalletId)
    } catch (err) {
      setReverseError(isApiError(err) ? err.message : 'Could not reverse this transaction.')
      setReversePhase('confirming')
    }
  }

  const now = new Date()
  const buckets = PERIODS.map((period) => ({
    period,
    txs: items.filter((tx) => periodOf(tx.postedAtUtc, now) === period),
  })).filter((bucket) => bucket.txs.length > 0)

  const nameOf = (accountId: string) =>
    wallets.find((w) => w.id === accountId)?.displayName ?? null

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <div>
          <h1 className={styles.title}>Activity</h1>
          <p className={styles.subtitle}>
            The journal, not a bank statement — open any row to see both ledger legs. This
            wallet&apos;s own leg is highlighted; the counterparty leg is never hidden.
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

      {(selectedWalletId ? status === 'loading' && items.length === 0 : walletsStatus === 'loading') && (
        <div className={styles.block}>
          <SkeletonList rows={5} label="Loading activity" />
        </div>
      )}

      {selectedWalletId && status === 'error' && (
        <div className={styles.block}>
          <div className={styles.stack}>
            <ErrorBanner message={error ?? 'Could not load activity.'} />
            <Button onClick={() => load(selectedWalletId)}>Retry</Button>
          </div>
        </div>
      )}

      {selectedWalletId && status === 'success' && items.length === 0 && (
        <div className={styles.block}>
          <EmptyState
            title="No activity yet"
            description="Fund this wallet or make a transfer to see entries here."
          />
        </div>
      )}

      {status !== 'error' && items.length > 0 && selectedWallet && buckets.map(({ period, txs }) => (
        <section key={period} className={['glass', styles.block].join(' ')} aria-labelledby={`period-${period}`}>
          <h2 id={`period-${period}`} className={styles.blockTitle}>
            {period}
            <span className={styles.count}>{txs.length}</span>
          </h2>
          <ol className={styles.list}>
            {txs.map((tx, idx) => {
              const view = describeTx(tx, selectedWallet.id)
              const isReversed = reversedTransactionIds.has(tx.id)
              const rewinding = rewindId === tx.id
              const canReverse = tx.type !== 'Reversal' && !isReversed
              const isConfirming = reversingId === tx.id
              const isOpen = expandedId === tx.id
              const counterId = view.counterEntry?.accountId
              const counterparty =
                tx.type === 'SimulatedFunding'
                  ? 'Simulated funding'
                  : counterId
                    ? (nameOf(counterId) ?? shortId(counterId))
                    : '—'
              const positive = view.signedMinorUnits >= 0
              const ownDirection = view.ownDirection
              const counterDirection = ownDirection === 'Debit' ? 'Credit' : 'Debit'
              return (
                  <motion.li
                    key={tx.id}
                    layout={!reduce}
                    className={styles.item}
                    initial={reduce ? { opacity: 0 } : { opacity: 0, y: 18 }}
                    animate={{ opacity: isReversed ? 0.78 : 1, y: 0 }}
                    transition={{ duration: 0.4, delay: Math.min(idx, 10) * 0.05, ease: [0.22, 1, 0.36, 1] }}
                  >
                    <button
                      type="button"
                      className={styles.row}
                      aria-expanded={isOpen}
                      aria-controls={`legs-${tx.id}`}
                      onClick={() => setExpandedId(isOpen ? null : tx.id)}
                    >
                      <TxIcon kind={view.kind} />
                      <span className={styles.rowText}>
                        <span className={styles.rowTitle}>
                          {view.label}
                          <span className={styles.rowCounter}> · {counterparty}</span>
                        </span>
                        <span className={styles.rowMeta}>
                          <time dateTime={tx.postedAtUtc}>
                            {new Date(tx.postedAtUtc).toLocaleString()}
                          </time>
                          <Badge variant="neutral">{tx.type}</Badge>
                          <Badge variant="neutral">{selectedWallet.currency}</Badge>
                          {isReversed ? (
                            <motion.span
                              key="reversed"
                              initial={reduce || !rewinding ? { opacity: 0 } : { scale: 0.3, opacity: 0, rotate: -12 }}
                              animate={{ scale: 1, opacity: 1, rotate: 0 }}
                              transition={{ type: 'spring', stiffness: 420, damping: 18, delay: rewinding ? 0.6 : 0 }}
                              className={styles.badgePop}
                            >
                              <Badge variant="danger">Reversed</Badge>
                            </motion.span>
                          ) : (
                            <Badge variant="credit">Posted</Badge>
                          )}
                        </span>
                      </span>
                      <motion.span
                        className={[styles.rowAmount, 'amount', positive ? styles.positive : ''].join(' ')}
                        initial={false}
                        animate={
                          isReversed
                            ? { x: reduce ? 0 : rewinding ? [0, -34, -10] : -10, opacity: 0.55 }
                            : { x: 0, opacity: 1 }
                        }
                        transition={{ duration: 0.35, ease: 'easeOut' }}
                      >
                        {formatSignedAmount(view.signedMinorUnits, selectedWallet.currency, positive ? 1 : -1)}
                        {isReversed && (
                          <motion.i
                            className={styles.strike}
                            aria-hidden="true"
                            initial={{ scaleX: rewinding ? 0 : 1 }}
                            animate={{ scaleX: 1 }}
                            transition={{ delay: rewinding ? 0.3 : 0, duration: 0.25, ease: 'easeOut' }}
                          />
                        )}
                      </motion.span>
                      <ChevronDown
                        size={18}
                        aria-hidden="true"
                        className={[styles.chevron, isOpen ? styles.chevronOpen : ''].join(' ')}
                      />
                    </button>
                    <AnimatePresence initial={false}>
                    {isOpen && (
                      <motion.div
                        key="detail"
                        id={`legs-${tx.id}`}
                        className={styles.detailWrap}
                        initial={reduce ? { opacity: 0 } : { opacity: 0, y: -10 }}
                        animate={{ opacity: 1, y: 0 }}
                        exit={{ opacity: 0, transition: { duration: 0.12 } }}
                        transition={{ duration: 0.25, ease: 'easeOut' }}
                      >
                      <div className={styles.detail}>
                        <div className={styles.entryId}>
                          <span className="mono">{shortId(tx.id)}</span>
                          <CopyButton value={tx.id} label="transaction ID" />
                        </div>
                        {tx.type === 'Reversal' && tx.reversalOfTransactionId && (
                          <p className={styles.reversalNote}>
                            Reverses <span className="mono">{shortId(tx.reversalOfTransactionId)}</span>
                            <CopyButton
                              value={tx.reversalOfTransactionId}
                              label="reversed transaction ID"
                            />
                          </p>
                        )}
                        <div className={styles.legs}>
                          <div className={[styles.leg, styles.legOwn].join(' ')}>
                            <Badge variant="neutral">
                              {ownDirection === 'Debit' ? '↗ Debit' : '↙ Credit'}
                            </Badge>
                            <span className={styles.legAccount}>This wallet</span>
                            <span className={'amount ' + (ownDirection === 'Debit' ? styles.positive : '')}>
                              {view.ownEntry
                                ? `${ownDirection === 'Debit' ? '+' : '-'}${formatAmount(
                                    view.ownEntry.amountMinorUnits,
                                    view.ownEntry.currency,
                                  )}`
                                : '—'}
                            </span>
                          </div>
                          <div className={styles.leg}>
                            <Badge variant="neutral">
                              {counterDirection === 'Debit' ? '↗ Debit' : '↙ Credit'}
                            </Badge>
                            <span className={styles.legAccount + ' mono'}>
                              {view.counterEntry?.accountId ?? '—'}
                            </span>
                            <span className={'amount ' + (counterDirection === 'Debit' ? styles.positive : '')}>
                              {view.counterEntry
                                ? `${counterDirection === 'Debit' ? '+' : '-'}${formatAmount(
                                    view.counterEntry.amountMinorUnits,
                                    view.counterEntry.currency,
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
                            <motion.div
                              className={styles.reverseConfirm}
                              initial={reduce ? { opacity: 0 } : { opacity: 0, y: 8 }}
                              animate={{ opacity: 1, y: 0 }}
                              transition={{ duration: 0.25 }}
                            >
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
                            </motion.div>
                          )}
                        </div>
                      </div>
                      </motion.div>
                    )}
                    </AnimatePresence>
                  </motion.li>
              )
            })}
          </ol>
        </section>
      ))}

      {status === 'success' && hasMore && (
        <div className={styles.footer}>
          <Button onClick={loadMore} isLoading={isLoadingMore}>
            Load more
          </Button>
        </div>
      )}
    </div>
  )
}
