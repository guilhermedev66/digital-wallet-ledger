import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { motion, useReducedMotion } from 'motion/react'
import { ArrowDownToLine, ArrowUpFromLine, Coins } from 'lucide-react'
import { AmountDisplay } from '../../components/AmountDisplay'
import { AnimatedDigits } from '../../components/AnimatedDigits'
import { FlightCoin } from '../../components/FlightCoin'
import { SuccessCheck } from '../../components/SuccessCheck'
import { WalletCreditCard } from '../../components/WalletCreditCard'
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

const QUICK_FRACTIONS: { value: string; label: string; fraction: number | null }[] = [
  { value: '25', label: '25%', fraction: 0.25 },
  { value: '50', label: '50%', fraction: 0.5 },
  { value: 'max', label: 'MAX', fraction: null },
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
  const [quick, setQuick] = useState('none')
  const reduce = useReducedMotion()
  const [arrived, setArrived] = useState(false)
  const routeRef = useRef<HTMLDivElement>(null)
  const fromCardRef = useRef<HTMLDivElement>(null)
  const toCardRef = useRef<HTMLDivElement>(null)

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

  function applyQuickFraction(value: string) {
    const fraction = QUICK_FRACTIONS.find((q) => q.value === value)?.fraction
    if (!fromWallet || fraction === undefined) return
    setQuick(value)
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
    setQuick('none')
    setArrived(false)
    setToWalletId('')
    setIdempotencyKey(crypto.randomUUID())
  }

  // M8.2: the outlined-secondary rung of the Confirm node's 3-tier action hierarchy
  // (Brex "Cookie Consent Dialog" pattern, see M8.2_RECIPE.md TRANSFER row). A
  // non-destructive way to back out of an in-progress entry without touching the
  // still-valid idempotency key - only the fields the user was actively filling in.
  function clearForm() {
    setAmount('')
    setQuick('none')
    setToWalletId('')
    setError(null)
  }

  // Debit-normal: money leaving the source posts as a Credit entry, money arriving at the
  // destination posts as a Debit entry (see mockClient.transfer / TransferHandler).
  const sourceCreditEntry = completedTransaction?.entries.find((e) => e.direction === 'Credit')
  const destinationDebitEntry = completedTransaction?.entries.find((e) => e.direction === 'Debit')

  const currency = fromWallet?.currency ?? 'USD'
  const showManifest = hasValidAmount && Boolean(fromWalletId) && toWalletId.trim().length > 0 && !knownCurrencyMismatch
  const destAfter =
    toWallet && hasValidAmount ? toWallet.balanceMinorUnits + minorUnits : null
  const routeReady = showManifest && !exceedsBalance

  return (
    <div className={styles.page}>
      <div className={styles.header}>
        <h1 className={styles.title}>Transfer</h1>
        <p className={styles.subtitle}>
          Move money between wallets of the same currency. Every transfer posts two ledger legs
          atomically.
        </p>
      </div>

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

      {wallets.length > 0 && (
        <>
          {phase === 'success' && completedTransaction ? (
            <>
              {/* The coin flies source -> destination; the destination card hops and glows on arrival. */}
              <section className={['glass', styles.hero].join(' ')} aria-label="Transfer route">
                <div className={styles.route} ref={routeRef} style={{ position: 'relative' }}>
                  <div className={styles.miniCard} ref={fromCardRef}>
                    {fromWallet && <WalletCreditCard wallet={fromWallet} compact />}
                  </div>
                  <div className={styles.arrow} aria-hidden="true">
                    <span className={styles.arrowLine} />
                    <svg viewBox="0 0 24 24" width="26" height="26" className={styles.arrowHeadReady}>
                      <path d="M5 12h14M13 6l6 6-6 6" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </div>
                  <motion.div
                    className={styles.miniCard}
                    ref={toCardRef}
                    animate={(arrived || reduce) && !reduce ? { y: [0, -16, 0], scale: [1, 1.05, 1] } : undefined}
                    transition={{ type: 'spring', stiffness: 420, damping: 12 }}
                    style={{ position: 'relative' }}
                  >
                    {toWallet ? (
                      <WalletCreditCard wallet={toWallet} compact />
                    ) : (
                      <div className={styles.destPlaceholder}>
                        <span className={styles.destTitle}>External wallet</span>
                        <span className={styles.destId + ' mono'}>{destinationDebitEntry?.accountId}</span>
                      </div>
                    )}
                    <motion.span
                      className={styles.arriveGlow}
                      aria-hidden="true"
                      initial={{ opacity: 0 }}
                      animate={{ opacity: arrived || reduce ? [0, 1, 0] : 0 }}
                      transition={{ duration: reduce ? 0.6 : 0.9, ease: 'easeOut' }}
                    />
                  </motion.div>
                  <FlightCoin
                    containerRef={routeRef}
                    fromRef={fromCardRef}
                    toRef={toCardRef}
                    onArrive={() => setArrived(true)}
                  />
                </div>
              </section>
            <motion.div
              className={styles.receiptPanel}
              initial={reduce ? false : { opacity: 0, y: 24, scale: 0.97 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              transition={{ duration: 0.5, delay: reduce ? 0 : 0.45, ease: [0.22, 1, 0.36, 1] }}
            >
              <div className={styles.sealed} role="status">
                <SuccessCheck />
                <p className={styles.sealedBanner}>Transaction sealed &amp; posted to ledger</p>
                <AmountDisplay
                  minorUnits={sourceCreditEntry?.amountMinorUnits ?? 0}
                  currency={sourceCreditEntry?.currency ?? 'USD'}
                  className={styles.sealedAmount}
                />
              </div>
              <table className={tableStyles.table}>
                <thead>
                  <tr>
                    <th>Leg</th>
                    <th>Account</th>
                    <th className={tableStyles.numeric}>Amount</th>
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
            </motion.div>
            </>
          ) : (
            <>
              {/* Hero: huge typed amount, quick tabs, source -> destination mini cards */}
              <section className={['glass', styles.hero].join(' ')} aria-label="Transfer summary">
                <span className={styles.heroLabel}>You send</span>
                <div className={styles.heroAmount}>
                  <span className={styles.heroCurrency}>{currency}</span>
                  <AnimatedDigits text={amount} placeholder="0.00" />
                </div>
                <div className={styles.quickWrap} role="tablist" aria-label="Quick amounts">
                  {QUICK_FRACTIONS.map((q) => (
                    <button
                      key={q.value}
                      type="button"
                      role="tab"
                      aria-selected={quick === q.value}
                      className={[styles.quickTab, quick === q.value ? styles.quickTabActive : ''].join(' ')}
                      onClick={() => applyQuickFraction(q.value)}
                      disabled={phase === 'submitting' || !fromWallet}
                    >
                      {quick === q.value && (
                        <motion.span
                          layoutId="quick-pill"
                          className={styles.quickPill}
                          transition={reduce ? { duration: 0 } : { type: 'spring', stiffness: 500, damping: 36 }}
                        />
                      )}
                      <span className={styles.quickText}>{q.label}</span>
                    </button>
                  ))}
                </div>

                <div className={styles.route}>
                  <div className={styles.miniCard}>
                    {fromWallet && <WalletCreditCard wallet={fromWallet} compact />}
                  </div>
                  <div className={styles.arrow} aria-hidden="true">
                    <span className={styles.arrowLine} />
                    {routeReady && !reduce && <span className={styles.arrowPulse} />}
                    <svg viewBox="0 0 24 24" width="26" height="26" className={routeReady ? styles.arrowHeadReady : styles.arrowHead}>
                      <path d="M5 12h14M13 6l6 6-6 6" fill="none" stroke="currentColor" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </div>
                  <div className={styles.miniCard}>
                    {toWallet ? (
                      <WalletCreditCard wallet={toWallet} compact />
                    ) : (
                      <div className={styles.destPlaceholder}>
                        <span className={styles.destTitle}>
                          {toWalletId.trim() ? 'External wallet' : 'Destination'}
                        </span>
                        <span className={styles.destId + ' mono'}>
                          {toWalletId.trim() ? toWalletId.trim() : 'Paste a wallet ID below'}
                        </span>
                      </div>
                    )}
                  </div>
                </div>
              </section>

              <div className={styles.layout}>
                <form onSubmit={handleSubmit} noValidate className={['glass', styles.card].join(' ')}>
                  {error && <ErrorBanner message={error} />}

                  <section className={styles.section} aria-labelledby="tf-from">
                    <h2 id="tf-from" className={styles.sectionLabel}>
                      From
                    </h2>
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
                        <div className={styles.pill + ' ' + styles.pillActive}>
                          <span className={styles.pillCurrency}>{fromWallet.currency}</span>
                          {fromWallet.displayName ?? 'Untitled wallet'}
                        </div>
                      )
                    )}
                    {fromWallet && (
                      <div className={styles.nodeMeta}>
                        <span className="mono">{fromWallet.id}</span>
                        <span className="amount">
                          Avail {formatAmount(fromWallet.balanceMinorUnits, fromWallet.currency)}
                        </span>
                      </div>
                    )}
                  </section>

                  <section className={styles.section} aria-label="Amount">
                    <LabelInput
                      label={`Amount${fromWallet ? ` (${fromWallet.currency})` : ''}`}
                      placeholder="0.00"
                      inputMode="decimal"
                      required
                      value={amount}
                      onChange={(e) => {
                        setAmount(e.target.value)
                        setQuick('none')
                      }}
                      disabled={phase === 'submitting'}
                      prefixSlot={fromWallet?.currency}
                      error={
                        exceedsBalance
                          ? `Exceeds available balance (${formatAmount(fromWallet!.balanceMinorUnits, fromWallet!.currency)} max).`
                          : undefined
                      }
                    />
                  </section>

                  <section className={styles.section} aria-labelledby="tf-to">
                    <h2 id="tf-to" className={styles.sectionLabel}>
                      To
                    </h2>
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
                        {knownCurrencyMatch && <Badge variant="credit">✓ {toWallet?.currency} match</Badge>}
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
                  </section>

                  <section className={styles.section} aria-labelledby="tf-confirm">
                    <h2 id="tf-confirm" className={styles.sectionLabel}>
                      Confirm
                    </h2>
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
                  </section>
                </form>

                <aside className={['glass', styles.manifest].join(' ')} aria-labelledby="manifest-title" aria-live="polite">
                  <div className={styles.manifestHead}>
                    <h2 id="manifest-title" className={styles.manifestTitle}>
                      Double-entry manifest
                    </h2>
                    <span className={styles.manifestSub}>Exactly these legs post, atomically</span>
                  </div>
                  <ul className={styles.rows}>
                    <li className={styles.mRow}>
                      <span className={[styles.mIcon, styles.mIconOut].join(' ')} aria-hidden="true">
                        <ArrowUpFromLine size={16} />
                      </span>
                      <div className={styles.mText}>
                        <span className={styles.mLabel}>Credit · source</span>
                        <span className={styles.mSub}>{fromWallet?.displayName ?? 'Source wallet'}</span>
                      </div>
                      <div className={styles.mValues}>
                        <span className={styles.mValue + ' amount'}>
                          {showManifest ? (
                            <>
                              -<AmountDisplay minorUnits={minorUnits!} currency={currency} />
                            </>
                          ) : (
                            '—'
                          )}
                        </span>
                        <span className={styles.mVar + ' amount'}>
                          {showManifest && fromWallet && remainingAfter !== null ? (
                            <>
                              {formatAmount(fromWallet.balanceMinorUnits, currency)} →{' '}
                              <AmountDisplay minorUnits={remainingAfter} currency={currency} />
                            </>
                          ) : (
                            ''
                          )}
                        </span>
                      </div>
                    </li>
                    <li className={styles.mRow}>
                      <span className={[styles.mIcon, styles.mIconIn].join(' ')} aria-hidden="true">
                        <ArrowDownToLine size={16} />
                      </span>
                      <div className={styles.mText}>
                        <span className={styles.mLabel}>Debit · destination</span>
                        <span className={styles.mSub}>
                          {toWallet?.displayName ??
                            (toWalletId.trim() ? 'External wallet' : 'Destination wallet')}
                        </span>
                      </div>
                      <div className={styles.mValues}>
                        <span className={styles.mValue + ' amount ' + styles.mPositive}>
                          {showManifest ? (
                            <>
                              +<AmountDisplay minorUnits={minorUnits!} currency={currency} />
                            </>
                          ) : (
                            '—'
                          )}
                        </span>
                        <span className={styles.mVar + ' amount'}>
                          {showManifest && toWallet && destAfter !== null ? (
                            <>
                              {formatAmount(toWallet.balanceMinorUnits, currency)} →{' '}
                              <AmountDisplay minorUnits={destAfter} currency={currency} />
                            </>
                          ) : showManifest ? (
                            'balance not visible'
                          ) : (
                            ''
                          )}
                        </span>
                      </div>
                    </li>
                    <li className={styles.mRow}>
                      <span className={styles.mIcon} aria-hidden="true">
                        <Coins size={16} />
                      </span>
                      <div className={styles.mText}>
                        <span className={styles.mLabel}>Currency</span>
                        <span className={styles.mSub}>Both legs, same currency</span>
                      </div>
                      <div className={styles.mValues}>
                        <span className={styles.mValue}>{currency}</span>
                        <span className={styles.mVar}>
                          {knownCurrencyMatch ? 'match ✓' : knownCurrencyMismatch ? 'mismatch' : ''}
                        </span>
                      </div>
                    </li>
                  </ul>
                  <div className={styles.zeroSumStrip}>
                    <span>Σ Debits = Σ Credits</span>
                    <span className={styles.zeroSumOk + ' amount'}>
                      {showManifest ? `${formatAmount(0, currency)} difference · balanced` : 'waiting for input'}
                    </span>
                  </div>
                </aside>
              </div>
            </>
          )}
        </>
      )}
    </div>
  )
}
