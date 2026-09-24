import { useEffect, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { apiClient, isApiError, type Transaction } from '../../api'
import { Badge } from '../../components/Badge'
import { Button } from '../../components/Button'
import { CopyButton } from '../../components/CopyButton'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import { FlowButton } from '../../components/FlowButton'
import { LabelInput } from '../../components/LabelInput'
import tableStyles from '../../components/Table.module.css'
import { useWallets } from '../../hooks/useWallets'
import { formatAmount, parseAmountToMinorUnits } from '../../lib/money'
import styles from './TransferPage.module.css'

type Phase = 'form' | 'submitting' | 'success'

const QUICK_FRACTIONS: { label: string; fraction: number | null }[] = [
  { label: '25%', fraction: 0.25 },
  { label: '50%', fraction: 0.5 },
  { label: 'MAX', fraction: null },
]

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
  const remainingAfter =
    hasValidAmount && fromWallet !== undefined ? fromWallet.balanceMinorUnits - minorUnits : null
  // Only checkable client-side when the destination happens to be one of the caller's own
  // wallets (its currency is visible); an external destination's currency is unknown until
  // the backend responds, so this is a proactive hint, not the authoritative check.
  const knownCurrencyMismatch =
    fromWallet !== undefined && toWallet !== undefined && fromWallet.currency !== toWallet.currency
  const knownCurrencyMatch =
    fromWallet !== undefined && toWallet !== undefined && fromWallet.currency === toWallet.currency
  const destinationUnresolved =
    toWalletId.trim().length > 0 && toWallet === undefined
  const canSubmit =
    Boolean(fromWalletId) &&
    toWalletId.trim().length > 0 &&
    hasValidAmount &&
    !exceedsBalance &&
    !knownCurrencyMismatch

  function applyQuickFraction(fraction: number | null) {
    if (!fromWallet) return
    const targetMinorUnits =
      fraction === null ? fromWallet.balanceMinorUnits : Math.floor(fromWallet.balanceMinorUnits * fraction)
    setAmount((targetMinorUnits / 100).toFixed(2))
  }

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

  // M8.2: the outlined-secondary rung of the Confirm node's 3-tier action hierarchy
  // (Brex "Cookie Consent Dialog" pattern, see M8.2_RECIPE.md TRANSFER row). A
  // non-destructive way to back out of an in-progress entry without touching the
  // still-valid idempotency key - only the fields the user was actively filling in.
  function clearForm() {
    setAmount('')
    setToWalletId('')
    setError(null)
  }

  // Debit-normal: money leaving the source posts as a Credit entry, money arriving at the
  // destination posts as a Debit entry (see mockClient.transfer / TransferHandler).
  const sourceCreditEntry = completedTransaction?.entries.find((e) => e.direction === 'Credit')
  const destinationDebitEntry = completedTransaction?.entries.find((e) => e.direction === 'Debit')

  return (
    <div>
      <h1 className={styles.title}>Transfer</h1>

      {walletsStatus === 'error' && (
        <div className={styles.stateBox}>
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
            <div className={styles.receiptPanel}>
              <p className={styles.sealedBanner}>
                <span aria-hidden="true">✓</span> Transaction sealed &amp; posted to ledger
              </p>
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
                    <td
                      data-label="Amount"
                      className={tableStyles.numeric + ' amount ' + styles.legAmountCredit}
                    >
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
                  <span className={styles.receiptLabel}>Posted (UTC)</span>
                  <span className="mono">
                    {new Date(completedTransaction.postedAtUtc).toLocaleString()}
                  </span>
                </div>
              </div>
              <div className={styles.receiptActions}>
                <Button variant="primary" onClick={startAnother}>
                  Initiate another transfer
                </Button>
              </div>
            </div>
          ) : (
            <div className={styles.composition}>
              <form onSubmit={handleSubmit} noValidate className={styles.connector}>
                {error && (
                  <div className={styles.formError}>
                    <ErrorBanner message={error} />
                  </div>
                )}

                {/* Node 1 — Source wallet */}
                <div className={styles.node}>
                  <div className={styles.nodeTrack} aria-hidden="true">
                    <span className={styles.nodeDot} />
                    <span className={styles.nodeLine} />
                  </div>
                  <div className={styles.nodeBody}>
                    <h2 className={styles.nodeLabel}>From</h2>

                    {wallets.length > 1 ? (
                      <div className={styles.pillGroup} role="radiogroup" aria-label="Source wallet">
                        {wallets.map((w) => (
                          <button
                            key={w.id}
                            type="button"
                            role="radio"
                            aria-checked={w.id === fromWalletId}
                            className={[styles.pill, w.id === fromWalletId ? styles.pillActive : '']
                              .filter(Boolean)
                              .join(' ')}
                            onClick={() => setFromWalletId(w.id)}
                            disabled={phase === 'submitting'}
                          >
                            <span className={styles.pillCurrency}>{w.currency}</span>
                            {w.displayName ?? 'Untitled wallet'}
                          </button>
                        ))}
                      </div>
                    ) : (
                      fromWallet && (
                        <div className={styles.walletChip}>
                          <span className={styles.chipCurrency}>{fromWallet.currency}</span>
                          {fromWallet.displayName ?? 'Untitled wallet'}
                        </div>
                      )
                    )}

                    {fromWallet && (
                      <div className={styles.nodeMeta}>
                        <span className="mono">{fromWallet.id}</span>
                        <span className={styles.nodeBalance + ' amount'}>
                          Avail {formatAmount(fromWallet.balanceMinorUnits, fromWallet.currency)}
                        </span>
                      </div>
                    )}
                  </div>
                </div>

                {/* Node 2 — Amount (the focal point of the flow) */}
                <div className={styles.node}>
                  <div className={styles.nodeTrack} aria-hidden="true">
                    <span className={[styles.nodeDot, styles.nodeDotAccent].join(' ')} />
                    <span className={styles.nodeLine} />
                  </div>
                  <div className={styles.nodeBody}>
                    <h2 className={styles.nodeLabel}>Amount</h2>
                    {/* Brex "Email Capture Input" pairing (M8.2_RECIPE.md TRANSFER row): the
                        field sits flush against its action with a 4-8px gap. The quick-fraction
                        chips now live in the amount field's own suffix slot instead of a second
                        row below a wrapping bordered panel - one hairline border (the input
                        well's) instead of a panel-in-panel double border. */}
                    <LabelInput
                      label={`Amount${fromWallet ? ` (${fromWallet.currency})` : ''}`}
                      placeholder="0.00"
                      inputMode="decimal"
                      required
                      scale="lg"
                      value={amount}
                      onChange={(e) => setAmount(e.target.value)}
                      disabled={phase === 'submitting'}
                      prefixSlot={fromWallet?.currency}
                      suffixSlot={
                        <div className={styles.quickChips}>
                          {QUICK_FRACTIONS.map((q) => (
                            <button
                              key={q.label}
                              type="button"
                              className={styles.quickChip}
                              onClick={() => applyQuickFraction(q.fraction)}
                              disabled={phase === 'submitting' || !fromWallet}
                            >
                              {q.label}
                            </button>
                          ))}
                        </div>
                      }
                      error={
                        exceedsBalance
                          ? `Exceeds available balance (${formatAmount(fromWallet!.balanceMinorUnits, fromWallet!.currency)} max).`
                          : undefined
                      }
                    />
                    {remainingAfter !== null && fromWallet && !exceedsBalance && (
                      <p className={styles.nodeHint}>
                        Remaining balance after transfer:{' '}
                        <span className="mono">{formatAmount(remainingAfter, fromWallet.currency)}</span>
                      </p>
                    )}
                  </div>
                </div>

                {/* Node 3 — Destination wallet */}
                <div className={styles.node}>
                  <div className={styles.nodeTrack} aria-hidden="true">
                    <span className={styles.nodeDot} />
                    <span className={styles.nodeLine} />
                  </div>
                  <div className={styles.nodeBody}>
                    <h2 className={styles.nodeLabel}>To</h2>
                    <LabelInput
                      label="Destination wallet ID"
                      placeholder="wallet_..."
                      required
                      mono
                      value={toWalletId}
                      onChange={(e) => setToWalletId(e.target.value)}
                      disabled={phase === 'submitting'}
                    />
                    {toWalletId.trim().length > 0 && (
                      <div className={styles.destPreview}>
                        {knownCurrencyMatch && (
                          <Badge variant="credit">✓ {toWallet?.currency} match</Badge>
                        )}
                        {knownCurrencyMismatch && (
                          <Badge variant="danger">
                            {toWallet?.currency} ≠ {fromWallet?.currency}
                          </Badge>
                        )}
                        {destinationUnresolved && <Badge variant="neutral">External wallet</Badge>}
                      </div>
                    )}
                    <p className={styles.nodeHint}>
                      Your own wallet or another account&apos;s — must be the same currency as the
                      source. Cross-currency transfers are rejected by ledger rules.
                    </p>
                    {knownCurrencyMismatch && toWallet && (
                      <ErrorBanner
                        message={`Currency mismatch: source is ${fromWallet?.currency}, destination is ${toWallet.currency}. Cross-currency transfers aren't supported.`}
                      />
                    )}
                  </div>
                </div>

                {/* Node 4 — Review */}
                <div className={styles.node}>
                  <div className={styles.nodeTrack} aria-hidden="true">
                    <span className={styles.nodeDot} />
                    <span className={styles.nodeLine} />
                  </div>
                  <div className={styles.nodeBody} aria-live="polite">
                    <h2 className={styles.nodeLabel}>Review</h2>
                    {hasValidAmount && fromWalletId && toWalletId.trim() && !knownCurrencyMismatch ? (
                      <div className={styles.review}>
                        <div className={styles.reviewSummary}>
                          <span className={styles.reviewChip}>
                            {fromWallet?.currency} · {fromWallet?.displayName ?? fromWalletId}
                          </span>
                          <span className={styles.reviewArrow} aria-hidden="true">
                            →
                          </span>
                          <span className={styles.reviewChip}>
                            {toWallet ? `${toWallet.currency} · ${toWallet.displayName ?? toWallet.id}` : 'External wallet'}
                          </span>
                        </div>
                        <p className={styles.reviewAmount + ' amount'}>
                          {formatAmount(minorUnits!, fromWallet?.currency ?? 'USD')}
                        </p>
                        <p className={styles.nodeHint}>
                          Exactly these two ledger legs will be posted atomically:
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
                              <td
                                data-label="Amount"
                                className={tableStyles.numeric + ' amount ' + styles.legAmountCredit}
                              >
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
                        <div className={styles.zeroSumStrip}>
                          <span>Σ Debits = Σ Credits</span>
                          <span className={styles.zeroSumOk}>$0.00 difference · balanced</span>
                        </div>
                      </div>
                    ) : (
                      <p className={styles.nodeHint}>
                        Fill in the destination and amount to see the exact entries before you
                        confirm.
                      </p>
                    )}
                  </div>
                </div>

                {/* Node 5 — Confirm */}
                <div className={styles.node}>
                  <div className={styles.nodeTrack} aria-hidden="true">
                    <span className={[styles.nodeDot, styles.nodeDotAccent].join(' ')} />
                  </div>
                  <div className={styles.nodeBody}>
                    <h2 className={styles.nodeLabel}>Confirm</h2>
                    {/* Brex "Cookie Consent Dialog" 3-button hierarchy (M8.2_RECIPE.md TRANSFER
                        row): filled primary / outlined secondary / text-link tertiary. Primary
                        is the FlowButton (Spell Pop Button press mechanic, unchanged). Outlined
                        secondary is "Clear form" - a real, non-destructive way to back out that
                        previously had no affordance beyond deleting each field by hand. The
                        text-link tertiary is the existing Regenerate action below, kept next to
                        the idempotency key it regenerates rather than pulled up into this row,
                        since its meaning depends on that adjacency. */}
                    <div className={styles.confirmActions}>
                      <FlowButton
                        type="submit"
                        className={styles.confirmButton}
                        isLoading={phase === 'submitting'}
                        loadingLabel="Posting atomic transaction…"
                        disabled={!canSubmit}
                      >
                        Post transaction to ledger →
                      </FlowButton>
                      {(amount.length > 0 || toWalletId.trim().length > 0) && (
                        <Button
                          type="button"
                          variant="secondary"
                          className={styles.clearButton}
                          onClick={clearForm}
                          disabled={phase === 'submitting'}
                        >
                          Clear form
                        </Button>
                      )}
                    </div>
                    <div className={styles.idCaption}>
                      <span className={styles.idLabel}>Idempotency</span>
                      <span className="mono">{idempotencyKey}</span>
                      <CopyButton value={idempotencyKey} label="idempotency key" />
                      <button
                        type="button"
                        className={styles.regenerate}
                        onClick={() => setIdempotencyKey(crypto.randomUUID())}
                        disabled={phase === 'submitting'}
                      >
                        ↻ Regenerate
                      </button>
                    </div>
                    <p className={styles.auditNote}>
                      Resubmission protection: an identical key replays the posted result instead of
                      double-posting.
                    </p>
                  </div>
                </div>
              </form>
            </div>
          )}
        </>
      )}
    </div>
  )
}
