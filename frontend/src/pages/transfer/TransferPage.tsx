import { useEffect, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { apiClient, isApiError, type Transaction } from '../../api'
import { Button } from '../../components/Button'
import { CopyButton } from '../../components/CopyButton'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import { Field } from '../../components/Field'
import tableStyles from '../../components/Table.module.css'
import { useWallets } from '../../hooks/useWallets'
import { formatAmount, parseAmountToMinorUnits } from '../../lib/money'
import styles from './TransferPage.module.css'

type Phase = 'form' | 'submitting' | 'success'

export function TransferPage() {
  const { wallets, status: walletsStatus, error: walletsError, refresh } = useWallets()
  const [searchParams] = useSearchParams()
  const preselectedWalletId = searchParams.get('wallet')

  const [fromWalletId, setFromWalletId] = useState('')
  const [toWalletId, setToWalletId] = useState('')
  const [amount, setAmount] = useState('')
  const [phase, setPhase] = useState<Phase>('form')
  const [error, setError] = useState<string | null>(null)
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())
  const [completedTransaction, setCompletedTransaction] = useState<Transaction | null>(null)

  useEffect(() => {
    if (fromWalletId || wallets.length === 0) return
    const preselected = preselectedWalletId && wallets.some((w) => w.id === preselectedWalletId)
    setFromWalletId(preselected ? preselectedWalletId! : wallets[0].id)
  }, [wallets, fromWalletId, preselectedWalletId])

  const fromWallet = wallets.find((w) => w.id === fromWalletId)
  const toWallet = wallets.find((w) => w.id === toWalletId.trim())
  const minorUnits = parseAmountToMinorUnits(amount)
  const hasValidAmount = minorUnits !== null && minorUnits > 0
  const exceedsBalance =
    hasValidAmount && fromWallet !== undefined && minorUnits > fromWallet.balanceMinorUnits
  // Only checkable client-side when the destination happens to be one of the caller's own
  // wallets (its currency is visible); an external destination's currency is unknown until
  // the backend responds, so this is a proactive hint, not the authoritative check.
  const knownCurrencyMismatch =
    fromWallet !== undefined && toWallet !== undefined && fromWallet.currency !== toWallet.currency
  const canSubmit =
    Boolean(fromWalletId) &&
    toWalletId.trim().length > 0 &&
    hasValidAmount &&
    !exceedsBalance &&
    !knownCurrencyMismatch

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    if (!hasValidAmount) {
      setError('Enter a valid amount, e.g. 25.00')
      return
    }
    if (!toWalletId.trim()) {
      setError('Enter a destination wallet ID.')
      return
    }

    setPhase('submitting')
    try {
      const tx = await apiClient.transfer({
        sourceWalletId: fromWalletId,
        destinationWalletId: toWalletId.trim(),
        amountMinorUnits: minorUnits!,
        idempotencyKey,
      })
      setCompletedTransaction(tx)
      setPhase('success')
      refresh()
    } catch (err) {
      setError(isApiError(err) ? err.message : 'Transfer failed. Please try again.')
      setPhase('form')
    }
  }

  function startAnother() {
    setCompletedTransaction(null)
    setPhase('form')
    setAmount('')
    setToWalletId('')
    setIdempotencyKey(crypto.randomUUID())
  }

  // Debit-normal: money leaving the source posts as a Credit entry, money arriving at the
  // destination posts as a Debit entry (see mockClient.transfer / TransferHandler).
  const sourceCreditEntry = completedTransaction?.entries.find((e) => e.direction === 'Credit')
  const destinationDebitEntry = completedTransaction?.entries.find((e) => e.direction === 'Debit')

  return (
    <div>
      <h1 className={styles.title}>Transfer</h1>

      {walletsStatus === 'error' && (
        <div style={{ display: 'grid', gap: 'var(--space-3)', maxWidth: 420 }}>
          <ErrorBanner message={walletsError ?? 'Could not load wallets.'} />
          <Button onClick={refresh}>Retry</Button>
        </div>
      )}

      {walletsStatus === 'success' && wallets.length === 0 && (
        <EmptyState
          title="No wallets to transfer from"
          description="Create a wallet on the dashboard first."
        />
      )}

      {walletsStatus === 'success' && wallets.length > 0 && (
        <>
          {phase === 'success' && completedTransaction ? (
            <div className={styles.panel}>
              <p className={styles.postedLabel}>✓ Posted</p>
              <table className={tableStyles.table}>
                <thead>
                  <tr>
                    <th>Leg</th>
                    <th>Account</th>
                    <th>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  <tr>
                    <td data-label="Leg">CREDIT</td>
                    <td data-label="Account" className="mono">
                      {sourceCreditEntry?.accountId}
                    </td>
                    <td data-label="Amount" className={tableStyles.numeric + ' amount'}>
                      -
                      {formatAmount(
                        sourceCreditEntry?.amountMinorUnits ?? 0,
                        sourceCreditEntry?.currency ?? 'USD',
                      )}
                    </td>
                  </tr>
                  <tr>
                    <td data-label="Leg">DEBIT</td>
                    <td data-label="Account" className="mono">
                      {destinationDebitEntry?.accountId}
                    </td>
                    <td data-label="Amount" className={tableStyles.numeric + ' amount'}>
                      +
                      {formatAmount(
                        destinationDebitEntry?.amountMinorUnits ?? 0,
                        destinationDebitEntry?.currency ?? 'USD',
                      )}
                    </td>
                  </tr>
                </tbody>
              </table>
              <div className={styles.receipt}>
                <div className={styles.receiptRow}>
                  <span className={styles.receiptLabel}>Transaction ID</span>
                  <span className="mono">{completedTransaction.id}</span>
                </div>
                <div className={styles.receiptRow}>
                  <span className={styles.receiptLabel}>Posted</span>
                  <span>{new Date(completedTransaction.postedAtUtc).toLocaleString()}</span>
                </div>
              </div>
              <Button variant="primary" onClick={startAnother}>
                Make another transfer
              </Button>
            </div>
          ) : (
            <div className={styles.layout}>
              <form className={styles.panel} onSubmit={handleSubmit} noValidate>
                {error && <ErrorBanner message={error} />}

                <div className={styles.selectField}>
                  <label className={styles.selectLabel} htmlFor="from-wallet">
                    From wallet
                  </label>
                  <select
                    id="from-wallet"
                    className={styles.select}
                    value={fromWalletId}
                    onChange={(e) => setFromWalletId(e.target.value)}
                    disabled={phase === 'submitting'}
                  >
                    {wallets.map((w) => (
                      <option key={w.id} value={w.id}>
                        {w.displayName ?? 'Untitled wallet'} ({w.currency})
                      </option>
                    ))}
                  </select>
                  {fromWallet && (
                    <span className={styles.availableBalance + ' amount'}>
                      Available: {formatAmount(fromWallet.balanceMinorUnits, fromWallet.currency)}
                    </span>
                  )}
                </div>

                <Field
                  label="To wallet ID"
                  placeholder="wallet_..."
                  required
                  value={toWalletId}
                  onChange={(e) => setToWalletId(e.target.value)}
                  disabled={phase === 'submitting'}
                  hint="Paste the destination wallet's ID (your own or another account's) — must be the same currency as the source wallet."
                />

                <Field
                  label={`Amount${fromWallet ? ` (${fromWallet.currency})` : ''}`}
                  placeholder="25.00"
                  inputMode="decimal"
                  required
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  disabled={phase === 'submitting'}
                  error={exceedsBalance ? 'Amount exceeds the source wallet\'s available balance.' : undefined}
                />

                {knownCurrencyMismatch && toWallet && (
                  <ErrorBanner
                    message={`Currency mismatch: source is ${fromWallet?.currency}, destination is ${toWallet.currency}. Cross-currency transfers aren't supported.`}
                  />
                )}

                <Button
                  type="submit"
                  variant="primary"
                  isLoading={phase === 'submitting'}
                  disabled={!canSubmit}
                >
                  Send transfer
                </Button>
              </form>

              <div className={styles.preview} aria-live="polite">
                <h2 className={styles.previewTitle}>Ledger preview</h2>
                {hasValidAmount && fromWalletId && toWalletId.trim() ? (
                  <>
                    <p className={styles.previewNote}>
                      Exactly these two entries will be posted atomically on confirm:
                    </p>
                    <table className={tableStyles.table}>
                      <tbody>
                        <tr>
                          <td data-label="Leg" className={styles.legLabel}>
                            CREDIT
                          </td>
                          <td data-label="Account" className="mono">
                            {fromWallet?.displayName ?? fromWalletId}
                          </td>
                          <td data-label="Amount" className={tableStyles.numeric + ' amount'}>
                            -{formatAmount(minorUnits!, fromWallet?.currency ?? 'USD')}
                          </td>
                        </tr>
                        <tr>
                          <td data-label="Leg" className={styles.legLabel}>
                            DEBIT
                          </td>
                          <td data-label="Account" className="mono">
                            {toWallet?.displayName ?? toWalletId.trim()}
                          </td>
                          <td data-label="Amount" className={tableStyles.numeric + ' amount'}>
                            +{formatAmount(minorUnits!, fromWallet?.currency ?? 'USD')}
                          </td>
                        </tr>
                      </tbody>
                    </table>
                  </>
                ) : (
                  <p className={styles.previewNote}>
                    Fill in the destination and amount to see the exact entries before you
                    confirm.
                  </p>
                )}
                <div className={styles.idempotencyRow}>
                  <span className={styles.previewLabel}>Idempotency key</span>
                  <div className={styles.idempotencyValue}>
                    <span className="mono">{idempotencyKey}</span>
                    <CopyButton value={idempotencyKey} label="idempotency key" />
                    <button
                      type="button"
                      className={styles.regenerate}
                      onClick={() => setIdempotencyKey(crypto.randomUUID())}
                      disabled={phase === 'submitting'}
                    >
                      Regenerate
                    </button>
                  </div>
                  <span className={styles.previewNote}>
                    Resubmitting with the same key is safe — it replays the original result
                    instead of double-posting.
                  </span>
                </div>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  )
}
