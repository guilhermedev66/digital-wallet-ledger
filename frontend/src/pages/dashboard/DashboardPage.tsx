import { Fragment, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { apiClient, isApiError, type Currency } from '../../api'
import { Button } from '../../components/Button'
import buttonStyles from '../../components/Button.module.css'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import { Field } from '../../components/Field'
import { SkeletonRows } from '../../components/Skeleton'
import tableStyles from '../../components/Table.module.css'
import { useWallets } from '../../hooks/useWallets'
import { formatAmount, parseAmountToMinorUnits } from '../../lib/money'
import styles from './DashboardPage.module.css'

export function DashboardPage() {
  const { wallets, status, error, refresh } = useWallets()
  const [isCreating, setIsCreating] = useState(false)
  const [fundingWalletId, setFundingWalletId] = useState<string | null>(null)

  return (
    <div>
      <div className={styles.header}>
        <div>
          <h1 className={styles.title}>Wallets</h1>
          <p className={styles.subtitle}>
            Balances are derived from posted ledger entries, never a stored field.
          </p>
        </div>
        <Button variant="primary" onClick={() => setIsCreating((v) => !v)}>
          {isCreating ? 'Cancel' : 'New wallet'}
        </Button>
      </div>

      <div className={styles.panel}>
        {isCreating && (
          <NewWalletForm
            onCreated={() => {
              setIsCreating(false)
              refresh()
            }}
            onCancel={() => setIsCreating(false)}
          />
        )}

        {status === 'loading' && (
          <div style={{ padding: 'var(--space-5)' }}>
            <SkeletonRows count={3} />
          </div>
        )}

        {status === 'error' && (
          <div style={{ padding: 'var(--space-5)', display: 'grid', gap: 'var(--space-3)' }}>
            <ErrorBanner message={error ?? 'Could not load wallets.'} />
            <Button onClick={refresh}>Retry</Button>
          </div>
        )}

        {status === 'success' && wallets.length === 0 && !isCreating && (
          <div style={{ padding: 'var(--space-5)' }}>
            <EmptyState
              title="No wallets yet"
              description="Create a wallet to start simulating deposits and transfers."
              action={
                <Button variant="primary" onClick={() => setIsCreating(true)}>
                  Create your first wallet
                </Button>
              }
            />
          </div>
        )}

        {status === 'success' && wallets.length > 0 && (
          <table className={tableStyles.table}>
            <thead>
              <tr>
                <th>Wallet</th>
                <th>Currency</th>
                <th>Balance</th>
                <th aria-label="Actions" />
              </tr>
            </thead>
            <tbody>
              {wallets.map((wallet) => (
                <Fragment key={wallet.id}>
                  <tr>
                    <td data-label="Wallet">
                      <span className={styles.walletName}>{wallet.displayName ?? 'Untitled wallet'}</span>
                      <span className={styles.walletMeta + ' mono'}>{wallet.id}</span>
                    </td>
                    <td data-label="Currency">{wallet.currency}</td>
                    <td data-label="Balance" className={tableStyles.numeric + ' amount'}>
                      {formatAmount(wallet.balanceMinorUnits, wallet.currency)}
                    </td>
                    <td data-label="Actions">
                      <div className={styles.rowActions}>
                        <Button
                          onClick={() =>
                            setFundingWalletId((id) => (id === wallet.id ? null : wallet.id))
                          }
                        >
                          {fundingWalletId === wallet.id ? 'Cancel' : 'Fund (demo)'}
                        </Button>
                        <Link
                          to={`/activity?wallet=${wallet.id}`}
                          className={[buttonStyles.button, buttonStyles.secondary].join(' ')}
                        >
                          Activity
                        </Link>
                      </div>
                    </td>
                  </tr>
                  {fundingWalletId === wallet.id && (
                    <tr key={`${wallet.id}-fund`}>
                      <td colSpan={4} style={{ padding: 0 }}>
                        <FundWalletForm
                          walletId={wallet.id}
                          currency={wallet.currency}
                          onFunded={() => {
                            setFundingWalletId(null)
                            refresh()
                          }}
                          onCancel={() => setFundingWalletId(null)}
                        />
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        )}
      </div>
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
        <label className="visually-hidden" htmlFor="new-wallet-currency">
          Currency
        </label>
        <select
          id="new-wallet-currency"
          className={styles.currencySelect}
          value={currency}
          onChange={(e) => setCurrency(e.target.value as Currency)}
          disabled={isSubmitting}
        >
          <option value="USD">USD</option>
          <option value="BRL">BRL</option>
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
    <form className={styles.inlineForm} onSubmit={handleSubmit} noValidate>
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
