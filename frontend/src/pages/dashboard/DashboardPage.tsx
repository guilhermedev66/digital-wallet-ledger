import { useState, type FormEvent } from 'react'
import { apiClient, isApiError, type Currency } from '../../api'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import { Field } from '../../components/Field'
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

export function DashboardPage() {
  const { wallets, status, error, refresh } = useWallets()
  const [isCreating, setIsCreating] = useState(false)
  const [fundingWalletId, setFundingWalletId] = useState<string | null>(null)

  const groups = groupByCurrency(wallets)

  return (
    <div>
      <div className={styles.header}>
        <div>
          <h1 className={styles.title}>Wallets</h1>
          <p className={styles.subtitle}>
            Balances are derived from posted ledger entries, never a stored field. Each
            wallet's balance stays in its own currency — nothing here is summed across
            currencies.
          </p>
        </div>
        <Button variant="primary" onClick={() => setIsCreating((v) => !v)}>
          {isCreating ? 'Cancel' : 'New wallet'}
        </Button>
      </div>

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
            <Button variant="primary" onClick={() => setIsCreating(true)}>
              Create your first wallet
            </Button>
          }
        />
      )}

      {status === 'success' && wallets.length > 0 && (
        <div className={styles.groups}>
          {groups.map(([currency, group]) => (
            <section key={currency} className={styles.group} aria-labelledby={`group-${currency}`}>
              <div className={styles.groupHeader}>
                <h2 id={`group-${currency}`} className={styles.groupTitle}>
                  {currency}
                </h2>
                {group.length > 1 && (
                  <span className={styles.groupSubtotal + ' amount'}>
                    {group.length} wallets · Total{' '}
                    {formatAmount(
                      group.reduce((sum, w) => sum + w.balanceMinorUnits, 0),
                      currency,
                    )}
                  </span>
                )}
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
          ))}
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
      <Field
        label={`Amount (${currency}) — simulated funding`}
        placeholder="25.00"
        inputMode="decimal"
        required
        value={amount}
        onChange={(e) => setAmount(e.target.value)}
        disabled={isSubmitting}
        error={error ?? undefined}
      />
      <div className={styles.inlineFormActions}>
        <Button type="submit" variant="primary" isLoading={isSubmitting}>
          Add demo funds
        </Button>
        <Button type="button" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
      </div>
    </form>
  )
}
