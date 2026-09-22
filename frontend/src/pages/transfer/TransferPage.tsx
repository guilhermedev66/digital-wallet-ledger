import { useEffect, useState, type FormEvent } from 'react'
import { apiClient, isApiError, type Transaction } from '../../api'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import { Field } from '../../components/Field'
import { useWallets } from '../../hooks/useWallets'
import { formatAmount, parseAmountToMinorUnits } from '../../lib/money'
import styles from './TransferPage.module.css'

type Phase = 'form' | 'submitting' | 'success'

export function TransferPage() {
  const { wallets, status: walletsStatus, error: walletsError, refresh } = useWallets()
  const [fromWalletId, setFromWalletId] = useState('')
  const [toWalletId, setToWalletId] = useState('')
  const [amount, setAmount] = useState('')
  const [phase, setPhase] = useState<Phase>('form')
  const [error, setError] = useState<string | null>(null)
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID())
  const [completedTransaction, setCompletedTransaction] = useState<Transaction | null>(null)

  useEffect(() => {
    if (!fromWalletId && wallets.length > 0) setFromWalletId(wallets[0].id)
  }, [wallets, fromWalletId])

  const fromWallet = wallets.find((w) => w.id === fromWalletId)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    const minorUnits = parseAmountToMinorUnits(amount)
    if (minorUnits === null || minorUnits <= 0) {
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
        amountMinorUnits: minorUnits,
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
        <div className={styles.layout}>
          {phase === 'success' && completedTransaction ? (
            <div className={styles.panel}>
              <p className={styles.postedLabel}>✓ Posted</p>
              <div className={styles.receipt}>
                <div className={styles.receiptRow}>
                  <span className={styles.receiptLabel}>Amount</span>
                  <span className="amount">
                    {formatAmount(
                      completedTransaction.entries[0]?.amountMinorUnits ?? 0,
                      fromWallet?.currency ?? 'USD',
                    )}
                  </span>
                </div>
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
                hint="Paste the destination wallet's ID (your own or another account's)."
              />

              <Field
                label={`Amount${fromWallet ? ` (${fromWallet.currency})` : ''}`}
                placeholder="25.00"
                inputMode="decimal"
                required
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                disabled={phase === 'submitting'}
              />

              <Button
                type="submit"
                variant="primary"
                isLoading={phase === 'submitting'}
                disabled={!fromWalletId}
              >
                Send transfer
              </Button>
            </form>
          )}
        </div>
      )}
    </div>
  )
}
