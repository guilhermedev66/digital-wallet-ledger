import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { AnimatePresence, motion, useReducedMotion, type Variants } from 'motion/react'
import { ArrowDownToLine, ArrowLeftRight, ArrowUpRight, Check, Copy, History, Plus, Sparkles, Undo2, Wallet as WalletIcon } from 'lucide-react'
import {
  apiClient,
  DEMO_WALLET_PREFIX,
  demoData,
  isApiError,
  type Currency,
} from '../../api'
import { AmountDisplay } from '../../components/AmountDisplay'
import { KpiCard } from '../../components/KpiCard'
import { TypeDonutCard } from '../../components/TypeDonutCard'
import { VolumeWaveCard } from '../../components/VolumeWaveCard'
import { Badge } from '../../components/Badge'
import { Button } from '../../components/Button'
import { EmptyState } from '../../components/EmptyState'
import { ErrorBanner } from '../../components/ErrorBanner'
import { Field } from '../../components/Field'
import { LabelInput } from '../../components/LabelInput'
import { PopButton } from '../../components/PopButton'
import { RevealTitle } from '../../components/RevealText'
import { GhostCard } from '../../components/GhostCard'
import { SkeletonCard, SkeletonList } from '../../components/Skeleton'
import { TxIcon } from '../../components/TxIcon'
import { WalletStack } from '../../components/WalletStack'
import type { Transaction } from '../../api'
import { CURRENCIES } from '../../lib/currency'
import { formatAmount, formatSignedAmount, parseAmountToMinorUnits } from '../../lib/money'
import { describeTx, shortId } from '../../lib/txView'
import { usePortfolioHistory } from '../../hooks/usePortfolioHistory'
import { useWalletHistory } from '../../hooks/useWalletHistory'
import { countWindows, depositedTotal, startOfToday, transfersTodayCount, typeCounts, volumeSeries, type TypeWindow } from '../../lib/portfolio'
import { useWallets, type WalletSummary } from '../../hooks/useWallets'
import styles from './DashboardPage.module.css'

function trendOf(w: TypeWindow | null): { text: string; direction: 'up' | 'down' | 'flat' } {
  if (!w) return { text: 'Loading…', direction: 'flat' }
  const direction = w.week > w.prevWeek ? 'up' : w.week < w.prevWeek ? 'down' : 'flat'
  return { text: `+${w.today} today · ${w.prevWeek} prev. week`, direction }
}

export function DashboardPage() {
  const { wallets, status, error, refresh } = useWallets()
  const [isCreating, setIsCreating] = useState(false)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [isLoadingDemo, setIsLoadingDemo] = useState(false)
  const reduce = useReducedMotion()

  const wallet = wallets.find((w) => w.id === selectedId) ?? wallets[0] ?? null
  const { txs } = usePortfolioHistory(wallets)
  const transfers = txs ? countWindows(txs, 'Transfer') : null
  const deposits = txs ? countWindows(txs, 'SimulatedFunding') : null
  const reversals = txs ? countWindows(txs, 'Reversal') : null
  const newToday = wallets.filter((w) => Date.parse(w.createdAtUtc) >= startOfToday()).length
  const hasDemo = wallets.some((w) => w.displayName?.startsWith(DEMO_WALLET_PREFIX))

  async function loadDemo() {
    if (!demoData) return
    setIsLoadingDemo(true)
    try {
      await demoData.load()
      refresh()
    } finally {
      setIsLoadingDemo(false)
    }
  }

  // Staggered entrance for the page's blocks (opacity only under reduced motion).
  const container: Variants = {
    hidden: {},
    show: { transition: { staggerChildren: reduce ? 0 : 0.09 } },
  }
  const item: Variants = {
    hidden: reduce ? { opacity: 0 } : { opacity: 0, y: 24 },
    show: { opacity: 1, y: 0, transition: { duration: 0.5, ease: [0.22, 1, 0.36, 1] } },
  }

  return (
    <div className={styles.page}>
      <div className={styles.pageHeader}>
        <div>
          <RevealTitle as="h1" className={styles.title}>
            Wallets
          </RevealTitle>
          <p className={styles.subtitle}>
            Balances come from posted ledger entries. Each wallet stays in its own currency —
            nothing is summed across currencies.
          </p>
        </div>
        <div className={styles.headerActions}>
          {hasDemo && (
            <Badge variant="attention" title="Generated sample data, mock mode only">
              <Sparkles size={12} aria-hidden="true" /> Demo data
            </Badge>
          )}
          <Button variant="primary" onClick={() => setIsCreating((v) => !v)}>
            {isCreating ? 'Cancel' : 'New wallet'}
          </Button>
        </div>
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

      {status === 'loading' && wallets.length === 0 && (
        <div className={styles.loading}>
          <SkeletonCard />
          <SkeletonList rows={3} />
        </div>
      )}

      {status === 'error' && (
        <div className={styles.errorBlock}>
          <ErrorBanner message={error ?? 'Could not load wallets.'} />
          <Button onClick={refresh}>Retry</Button>
        </div>
      )}

      {status === 'success' && wallets.length === 0 && !isCreating && (
        <EmptyState
          title="No wallets yet"
          description="Create a wallet to start posting simulated deposits and transfers — every balance you'll see from here on is computed live from the ledger entries you create, not stored anywhere."
          action={
            <div className={styles.emptyActions}>
              <GhostCard />
              <PopButton onClick={() => setIsCreating(true)}>Create your first wallet</PopButton>
              {demoData && (
                <Button onClick={loadDemo} isLoading={isLoadingDemo}>
                  <Sparkles size={14} aria-hidden="true" /> Load demo data
                </Button>
              )}
              {demoData && (
                <span className={styles.demoNote}>
                  Demo data is generated sample data. It exists only in mock mode.
                </span>
              )}
            </div>
          }
        />
      )}

      {wallet && (
        <motion.div variants={container} initial="hidden" animate="show" className={styles.stage}>
          <motion.div variants={item} className={styles.kpis}>
            <KpiCard
              label="Active wallets"
              value={wallets.filter((w) => w.entryCount > 0).length}
              Icon={WalletIcon}
              tone="mint"
              direction={newToday > 0 ? 'up' : 'flat'}
              trend={`+${newToday} created today · ${wallets.length} total`}
            />
            <KpiCard
              label="Transfers this week"
              value={transfers?.week ?? null}
              Icon={ArrowLeftRight}
              tone="violet"
              direction={trendOf(transfers).direction}
              trend={trendOf(transfers).text}
            />
            <KpiCard
              label="Deposits this week"
              value={deposits?.week ?? null}
              Icon={ArrowDownToLine}
              tone="teal"
              direction={trendOf(deposits).direction}
              trend={trendOf(deposits).text}
            />
            <KpiCard
              label="Reversals this week"
              value={reversals?.week ?? null}
              Icon={Undo2}
              tone="amber"
              direction={trendOf(reversals).direction}
              trend={trendOf(reversals).text}
            />
          </motion.div>
          <WalletPanel
            wallet={wallet}
            wallets={wallets}
            onSelect={setSelectedId}
            onFunded={refresh}
            item={item}
            portfolio={txs}
          />
        </motion.div>
      )}
    </div>
  )
}

function WalletPanel({
  wallet,
  wallets: allWallets,
  onSelect,
  onFunded,
  item,
  portfolio,
}: {
  wallet: WalletSummary
  wallets: WalletSummary[]
  onSelect: (id: string) => void
  onFunded: () => void
  item: Variants
  portfolio: Transaction[] | null
}) {
  const [fundingFor, setFundingFor] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const reduce = useReducedMotion()
  const { items, failed } = useWalletHistory(wallet)
  const isFunding = fundingFor === wallet.id
  const currency: Currency = wallet.currency

  async function copyId() {
    try {
      await navigator.clipboard.writeText(wallet.id)
      setCopied(true)
      setTimeout(() => setCopied(false), 1500)
    } catch {
      // Clipboard unavailable: the ID stays visible (last 4 on the card) and in Activity.
    }
  }

  const actionCircle =
    'grid size-[78px] max-[480px]:size-[62px] place-items-center rounded-full border border-[var(--color-border-strong)] bg-[color-mix(in_srgb,var(--color-surface)_70%,transparent)] text-[var(--color-text)] backdrop-blur-md transition duration-200 ease-out group-hover:-translate-y-2 group-hover:border-transparent group-hover:bg-[var(--color-accent)] group-hover:text-[var(--color-accent-contrast)] group-hover:shadow-[0_18px_34px_-12px_var(--color-accent)] group-active:translate-y-0 group-active:scale-90 group-focus-visible:-translate-y-2'
  const actionBase =
    'group flex cursor-pointer flex-col items-center gap-2.5 border-0 bg-transparent p-0 text-[var(--color-text)] no-underline'

  const nameOf = (accountId: string) =>
    allWallets.find((w) => w.id === accountId)?.displayName ?? shortId(accountId)

  const wave = portfolio ? volumeSeries(portfolio, currency) : null
  const counts = portfolio ? typeCounts(portfolio) : { deposit: 0, transfer: 0, reversal: 0 }

  return (
    <>
      <div className={styles.mainRow}>
        <div className={styles.leftCol}>
          <motion.div variants={item}>
            <WalletStack wallets={allWallets} activeId={wallet.id} onChange={onSelect} />
          </motion.div>
          <motion.section variants={item} className={styles.balanceBlock} aria-label="Balance">
        <span className={styles.balanceCaption}>
          {wallet.displayName ?? 'Untitled wallet'} · {currency}
        </span>
        <AmountDisplay
          minorUnits={wallet.balanceMinorUnits}
          currency={currency}
          className={styles.balance}
          flashOnIncrease
          resetKey={wallet.id}
        />
        <span className={styles.balanceMeta}>
          {wallet.entryCount} {wallet.entryCount === 1 ? 'entry' : 'entries'}
          {wallet.lastActivityAtUtc &&
            ` · last activity ${new Date(wallet.lastActivityAtUtc).toLocaleDateString()}`}
        </span>
      </motion.section>
          <motion.div variants={item} className={styles.actions} role="group" aria-label="Wallet actions">
        <button
          type="button"
          className={actionBase}
          onClick={() => setFundingFor(isFunding ? null : wallet.id)}
          aria-expanded={isFunding}
        >
          <span className={actionCircle}>
            <Plus size={26} aria-hidden="true" />
          </span>
          <span className={styles.actionLabel}>Deposit</span>
        </button>
        <Link to={`/transfer?wallet=${wallet.id}`} className={[actionBase, styles.actionLink].join(' ')}>
          <span className={actionCircle}>
            <ArrowUpRight size={26} aria-hidden="true" />
          </span>
          <span className={styles.actionLabel}>Send</span>
        </Link>
        <Link to={`/activity?wallet=${wallet.id}`} className={[actionBase, styles.actionLink].join(' ')}>
          <span className={actionCircle}>
            <History size={26} aria-hidden="true" />
          </span>
          <span className={styles.actionLabel}>Activity</span>
        </Link>
        <button type="button" className={actionBase} onClick={copyId}>
          <span className={actionCircle}>
            {copied ? <Check size={26} aria-hidden="true" /> : <Copy size={26} aria-hidden="true" />}
          </span>
          <span className={styles.actionLabel} aria-live="polite">
            {copied ? 'Copied' : 'Copy ID'}
          </span>
        </button>
      </motion.div>
          <AnimatePresence initial={false}>
        {isFunding && (
          <motion.div
            key="fund"
            className={styles.drawer}
            initial={reduce ? { opacity: 0 } : { opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, transition: { duration: 0.12 } }}
            transition={{ duration: 0.25, ease: 'easeOut' }}
          >
            <FundWalletForm
              walletId={wallet.id}
              currency={currency}
              onFunded={() => {
                setFundingFor(null)
                onFunded()
              }}
              onCancel={() => setFundingFor(null)}
            />
          </motion.div>
        )}
      </AnimatePresence>
        </div>
        <motion.div variants={item} className={styles.waveCol}>
          {wave ? (
            <VolumeWaveCard
              values={wave.values}
              labels={wave.labels}
              currency={currency}
              deposited={depositedTotal(portfolio!, currency)}
              transfersToday={transfersTodayCount(portfolio!, currency)}
            />
          ) : (
            <div className={['glass', styles.card].join(' ')}>
              <SkeletonList rows={4} label="Loading volume" />
            </div>
          )}
        </motion.div>
      </div>

      <motion.div variants={item} className={styles.lower}>
        <TypeDonutCard counts={counts} />
        <section className={['glass', styles.card].join(' ')} aria-labelledby="recent-title">
          <div className={styles.cardHead}>
            <h2 id="recent-title" className={styles.cardTitle}>
              Recent transactions
            </h2>
            <Link to={`/activity?wallet=${wallet.id}`} className={styles.cardLink}>
              See all
            </Link>
          </div>
          {items === null && !failed && <SkeletonList rows={4} />}
          {failed && <ErrorBanner message="Could not load recent transactions." />}
          {items !== null && items.length === 0 && (
            <p className={styles.cardEmpty}>No transactions yet. Deposit funds to get started.</p>
          )}
          {items !== null && items.length > 0 && (
            <ul className={styles.txList}>
              {items.slice(0, 6).map((tx, i) => {
                const view = describeTx(tx, wallet.id)
                const counterId = view.counterEntry?.accountId
                const counterparty =
                  tx.type === 'SimulatedFunding'
                    ? 'Simulated funding'
                    : counterId
                      ? nameOf(counterId)
                      : '—'
                const positive = view.signedMinorUnits >= 0
                return (
                  <motion.li
                    key={tx.id}
                    className={styles.txRow}
                    initial={reduce ? { opacity: 0 } : { opacity: 0, x: -16 }}
                    animate={{ opacity: 1, x: 0 }}
                    transition={{ delay: 0.25 + i * 0.07, duration: 0.4, ease: 'easeOut' }}
                  >
                    <TxIcon kind={view.kind} />
                    <div className={styles.txText}>
                      <span className={styles.txLabel}>{view.label}</span>
                      <span className={styles.txSub}>
                        {counterparty} · {new Date(tx.postedAtUtc).toLocaleDateString()}
                      </span>
                    </div>
                    <span className={[styles.txAmount, 'amount', positive ? styles.positive : ''].join(' ')}>
                      {formatSignedAmount(view.signedMinorUnits, currency, positive ? 1 : -1)}
                    </span>
                  </motion.li>
                )
              })}
            </ul>
          )}
        </section>
      </motion.div>
    </>
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
