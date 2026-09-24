import { useState, type FormEvent } from 'react'
import { apiClient, isApiError, type Currency } from '../../api'
import { Button } from '../../components/Button'
import { PopButton } from '../../components/PopButton'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import { Field } from '../../components/Field'
import { LabelInput } from '../../components/LabelInput'
import { SkeletonRows } from '../../components/Skeleton'
import { WalletCard } from '../../components/WalletCard'
import { CURRENCIES } from '../../lib/currency'
import { useWallets, type WalletSummary } from '../../hooks/useWallets'
import { formatAmount, parseAmountToMinorUnits } from '../../lib/money'
import styles from './DashboardPage.module.css'

function groupByCurrency(wallets: WalletSummary[]): Array<[Currency, WalletSummary[]]> {
  const groups = new Map<Currency, WalletSummary[]>()
  for (const wallet of wallets) {
    const group = groups.get(wallet.currency) ?? []
    group.push(wallet)
    groups.set(wallet.currency, group)
  }
  return Array.from(groups.entries())
}

// Newest lastActivityAtUtc across a set of wallets, or null if none has posted yet -
// real data derived from useWallets, used both for the portfolio-wide stat and for
// each currency group's own "last activity" line.
function latestActivity(wallets: WalletSummary[]): string | null {
  return wallets.reduce<string | null>((latest, w) => {
    if (!w.lastActivityAtUtc) return latest
    if (!latest || w.lastActivityAtUtc > latest) return w.lastActivityAtUtc
    return latest
  }, null)
}

export function DashboardPage() {
  const { wallets, status, error, refresh } = useWallets()
  const [isCreating, setIsCreating] = useState(false)
  const [fundingWalletId, setFundingWalletId] = useState<string | null>(null)

  const groups = groupByCurrency(wallets)
  const distinctCurrencies = groups.length
  const totalEntries = wallets.reduce((sum, w) => sum + w.entryCount, 0)
  const lastActivityAtUtc = latestActivity(wallets)

  return (
    <div>
      {/* Header row: title + the one primary action, decoupled from the stat band
          below it (was previously fused into a single flex row) so the page reads
          as "heading area" then "instrument band" then "content", not a nav-bar
          idiom with numbers stuffed into it. */}
      <div className={styles.pageHeader}>
        <h1 className={styles.commandTitle}>Wallets</h1>
        <Button variant="primary" onClick={() => setIsCreating((v) => !v)}>
          {isCreating ? 'Cancel' : 'New wallet'}
        </Button>
      </div>
      <p className={styles.subtitle}>
        Balances are derived from posted ledger entries, never a stored field. Each wallet's
        balance stays in its own currency — nothing here is summed across currencies.
      </p>

      {status === 'success' && wallets.length > 0 && (
        <div className={styles.statBand}>
          <span className={styles.statBandLabel}>Portfolio snapshot</span>
          <div className={styles.statGrid}>
            <div className={styles.statCell}>
              <span className={styles.statCellValue + ' mono'}>{wallets.length}</span>
              <span className={styles.statCellLabel}>
                {wallets.length === 1 ? 'Wallet' : 'Wallets'}
              </span>
            </div>
            <div className={styles.statCell}>
              <span className={styles.statCellValue + ' mono'}>{distinctCurrencies}</span>
              <span className={styles.statCellLabel}>
                {distinctCurrencies === 1 ? 'Currency' : 'Currencies'}
              </span>
            </div>
            <div className={styles.statCell}>
              <span className={styles.statCellValue + ' mono'}>{totalEntries}</span>
              <span className={styles.statCellLabel}>Posted entries</span>
            </div>
            <div className={styles.statCell}>
              <span className={styles.statCellValue + ' mono'}>
                {lastActivityAtUtc ? new Date(lastActivityAtUtc).toLocaleDateString() : '—'}
              </span>
              <span className={styles.statCellLabel}>Last activity</span>
            </div>
          </div>
        </div>
      )}

      {isCreating && (
        <div className={styles.createPanel}>
          <NewWalletForm
            onCreated={() => {
              setIsCreating(false)
              refresh()
            }}
            onCancel={() => setIsCreating(false)}
          />
        </div>
      )}

      {status === 'loading' && (
        <div className={styles.loading}>
          <SkeletonRows count={3} />
        </div>
      )}

      {status === 'error' && (
        <div style={{ display: 'grid', gap: 'var(--space-3)' }}>
          <ErrorBanner message={error ?? 'Could not load wallets.'} />
          <Button onClick={refresh}>Retry</Button>
        </div>
      )}

      {status === 'success' && wallets.length === 0 && !isCreating && (
        <EmptyState
          title="No wallets yet"
          description="Create a wallet to start posting simulated deposits and transfers — every balance you'll see from here on is computed live from the ledger entries you create, not stored anywhere."
          action={
            <PopButton onClick={() => setIsCreating(true)}>Create your first wallet</PopButton>
          }
        />
      )}

      {status === 'success' && wallets.length > 0 && (
        <div className={styles.groups}>
          {groups.map(([currency, group]) => {
            const groupEntryCount = group.reduce((sum, w) => sum + w.entryCount, 0)
            const groupLastActivity = latestActivity(group)
            return (
              <section
                key={currency}
                className={styles.group}
                aria-labelledby={`group-${currency}`}
              >
                {/* Brex "Feature Category Card": heading + body + anchor, subtle
                    separation by spacing alone, no visible border between the
                    header and the content below it — the divider that used to
                    sit here is gone; the band's own edge is the only boundary. */}
                <div className={styles.groupHeader}>
                  <div className={styles.groupHeading}>
                    <h2 id={`group-${currency}`} className={styles.groupTitle}>
                      {currency}
                    </h2>
                    <p className={styles.groupBody}>
                      {group.length} wallet {group.length === 1 ? 'node' : 'nodes'} ·{' '}
                      {groupEntryCount} posted {groupEntryCount === 1 ? 'entry' : 'entries'}
                      {groupLastActivity
                        ? ` · last activity ${new Date(groupLastActivity).toLocaleDateString()}`
                        : ''}
                    </p>
                  </div>
                  <div className={styles.groupTotalBlock}>
                    <span className={styles.groupTotalLabel}>Total {currency}</span>
                    <span className={styles.groupTotal + ' mono'}>
                      {formatAmount(
                        group.reduce((sum, w) => sum + w.balanceMinorUnits, 0),
                        currency,
                      )}
                    </span>
                  </div>
                </div>
                <div className={styles.grid}>
                  {group.map((wallet) => (
                    <WalletCard
                      key={wallet.id}
                      wallet={wallet}
                      isFunding={fundingWalletId === wallet.id}
                      onToggleFund={() =>
                        setFundingWalletId((id) => (id === wallet.id ? null : wallet.id))
                      }
                      fundingSlot={
                        fundingWalletId === wallet.id ? (
                          <FundWalletForm
                            walletId={wallet.id}
                            currency={wallet.currency}
                            onFunded={() => {
                              setFundingWalletId(null)
                              refresh()
                            }}
                            onCancel={() => setFundingWalletId(null)}
                          />
                        ) : undefined
                      }
                    />
                  ))}
                </div>
              </section>
            )
          })}
        </div>
      )}
    </div>
  )
}

function NewWalletForm({
  onCreated,
  onCancel,
}: {
  onCreated: () => void
  onCancel: () => void
}) {
  const [name, setName] = useState('')
  const [currency, setCurrency] = useState<Currency>('USD')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await apiClient.createWallet(currency, name)
      onCreated()
    } catch (err) {
      setError(isApiError(err) ? err.message : 'Could not create wallet.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form className={styles.inlineForm} onSubmit={handleSubmit} noValidate>
      <Field
        label="Wallet name"
        placeholder="e.g. Main wallet"
        required
        value={name}
        onChange={(e) => setName(e.target.value)}
        disabled={isSubmitting}
      />
      <div className={styles.currencyField}>
        <label className={styles.selectLabel} htmlFor="new-wallet-currency">
          Currency
        </label>
        <select
          id="new-wallet-currency"
          className={styles.currencySelect}
          value={currency}
          onChange={(e) => setCurrency(e.target.value as Currency)}
          disabled={isSubmitting}
        >
          {CURRENCIES.map((c) => (
            <option key={c.code} value={c.code}>
              {c.code} — {c.label}
            </option>
          ))}
        </select>
      </div>
      {error && <ErrorBanner message={error} />}
      <div className={styles.inlineFormActions}>
        <Button type="submit" variant="primary" isLoading={isSubmitting}>
          Create
        </Button>
        <Button type="button" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
      </div>
    </form>
  )
}

const DEPOSIT_PRESETS = [10000, 100000] // minor units: +$100.00, +$1,000.00 (currency-symbol-agnostic)

function FundWalletForm({
  walletId,
  currency,
  onFunded,
  onCancel,
}: {
  walletId: string
  currency: Currency
  onFunded: () => void
  onCancel: () => void
}) {
  const [amount, setAmount] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  function applyPreset(minorUnits: number) {
    setError(null)
    setAmount((minorUnits / 100).toFixed(2))
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    const minorUnits = parseAmountToMinorUnits(amount)
    if (minorUnits === null || minorUnits <= 0) {
      setError('Enter a valid amount, e.g. 25.00')
      return
    }
    setIsSubmitting(true)
    try {
      await apiClient.fundWallet(walletId, minorUnits, crypto.randomUUID())
      onFunded()
    } catch (err) {
      setError(isApiError(err) ? err.message : 'Could not fund wallet.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form className={styles.fundForm} onSubmit={handleSubmit} noValidate>
      <span className={styles.fundLabel}>Add funds ({currency}) — simulated</span>
      <div className={styles.presetRow}>
        {DEPOSIT_PRESETS.map((minorUnits) => (
          <button
            key={minorUnits}
            type="button"
            className={styles.presetChip}
            onClick={() => applyPreset(minorUnits)}
            disabled={isSubmitting}
          >
            +{formatAmount(minorUnits, currency)}
          </button>
        ))}
      </div>
      {/* Brex "Email Capture Input": field + attached submit button, 4-8px gap -
          the amount field and the single money-movement action are one visual
          unit, not a field followed by a detached button row. */}
      <div className={styles.depositPair}>
        <LabelInput
          label="Amount"
          placeholder="25.00"
          inputMode="decimal"
          required
          mono
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          disabled={isSubmitting}
          error={error ?? undefined}
          className={styles.depositInput}
        />
        <Button
          type="submit"
          variant="primary"
          isLoading={isSubmitting}
          className={styles.depositSubmit}
        >
          Add funds
        </Button>
      </div>
      <button
        type="button"
        className={styles.ghostCancel}
        onClick={onCancel}
        disabled={isSubmitting}
      >
        Cancel
      </button>
    </form>
  )
}
