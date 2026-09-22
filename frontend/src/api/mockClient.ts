import type {
  AccountReconciliation,
  ApiClient,
  AuthSession,
  Currency,
  PagedResult,
  ReconciliationReport,
  RegisteredUser,
  Transaction,
  TransferInput,
  UnbalancedTransaction,
  Wallet,
  WalletBalance,
} from './types'
import { ApiError } from './types'

const STORAGE_KEY = 'walletledger.mock.v2'
const SYSTEM_FUNDING_ACCOUNT_ID = 'system-funding-account'

interface MockUser {
  id: string
  email: string
  passwordHash: string
}

interface MockDb {
  users: MockUser[]
  wallets: Wallet[]
  transactions: Transaction[]
  idempotencyIndex: Record<string, string> // `${userId}:${idempotencyKey}` -> transactionId
}

function emptyDb(): MockDb {
  return { users: [], wallets: [], transactions: [], idempotencyIndex: {} }
}

function loadDb(): MockDb {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return emptyDb()
    return JSON.parse(raw) as MockDb
  } catch {
    return emptyDb()
  }
}

function saveDb(db: MockDb) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(db))
  } catch {
    // best-effort demo persistence; ignore quota/private-mode failures
  }
}

function delay(ms = 350): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

function id(prefix: string): string {
  return `${prefix}_${Math.random().toString(36).slice(2, 11)}`
}

async function hash(password: string): Promise<string> {
  const bytes = new TextEncoder().encode(password)
  const digest = await crypto.subtle.digest('SHA-256', bytes)
  return Array.from(new Uint8Array(digest))
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('')
}

function tokenFor(userId: string): string {
  return `mock.${userId}.${Date.now()}`
}

function userIdFromToken(token: string): string | null {
  const parts = token.split('.')
  return parts.length === 3 && parts[0] === 'mock' ? parts[1] : null
}

// Debit-normal for every account, matching the real backend (see MEMORY.md / API_CONTRACT.md):
// a Debit entry increases an account's derived balance, Credit decreases it.
function walletBalance(db: MockDb, walletId: string): number {
  let balance = 0
  for (const tx of db.transactions) {
    for (const entry of tx.entries) {
      if (entry.accountId !== walletId) continue
      balance += entry.direction === 'Debit' ? entry.amountMinorUnits : -entry.amountMinorUnits
    }
  }
  return balance
}

export class MockApiClient implements ApiClient {
  private db: MockDb = loadDb()
  private currentUserId: string | null = null

  private requireAuth(): string {
    if (!this.currentUserId) {
      throw new ApiError('Not authenticated', 401, 'unauthenticated')
    }
    return this.currentUserId
  }

  setAuthToken(token: string | null) {
    this.currentUserId = token ? userIdFromToken(token) : null
  }

  async register(email: string, password: string): Promise<RegisteredUser> {
    await delay()
    if (this.db.users.some((u) => u.email.toLowerCase() === email.toLowerCase())) {
      throw new ApiError('An account with this email already exists.', 409, 'email_taken')
    }
    if (password.length < 8) {
      throw new ApiError('Password must be at least 8 characters.', 422, 'weak_password')
    }
    const user: MockUser = { id: id('user'), email, passwordHash: await hash(password) }
    this.db.users.push(user)
    saveDb(this.db)
    return { userId: user.id }
  }

  async login(email: string, password: string): Promise<AuthSession> {
    await delay()
    const record = this.db.users.find((u) => u.email.toLowerCase() === email.toLowerCase())
    const passwordHash = await hash(password)
    if (!record || record.passwordHash !== passwordHash) {
      throw new ApiError('Invalid email or password.', 401, 'invalid_credentials')
    }
    this.currentUserId = record.id
    return { user: { id: record.id, email: record.email }, token: tokenFor(record.id) }
  }

  async listWallets(): Promise<Wallet[]> {
    await delay()
    const userId = this.requireAuth()
    return this.db.wallets.filter((w) => w.ownerUserId === userId)
  }

  async createWallet(currency: Currency, displayName?: string): Promise<Wallet> {
    await delay()
    const userId = this.requireAuth()
    const wallet: Wallet = {
      id: id('wallet'),
      ownerUserId: userId,
      currency,
      displayName: displayName?.trim() || null,
      createdAtUtc: new Date().toISOString(),
    }
    this.db.wallets.push(wallet)
    saveDb(this.db)
    return wallet
  }

  async getWallet(walletId: string): Promise<Wallet> {
    await delay()
    const userId = this.requireAuth()
    // Not-found and not-yours are indistinguishable - both 404 - matching the real API's
    // ownership convention (see MEMORY.md "Never a 403 anywhere in this API").
    const wallet = this.db.wallets.find((w) => w.id === walletId && w.ownerUserId === userId)
    if (!wallet) throw new ApiError('Wallet not found.', 404, 'not_found')
    return wallet
  }

  async getWalletBalance(walletId: string): Promise<WalletBalance> {
    await delay()
    const userId = this.requireAuth()
    const wallet = this.db.wallets.find((w) => w.id === walletId && w.ownerUserId === userId)
    if (!wallet) throw new ApiError('Wallet not found.', 404, 'not_found')
    return { walletId: wallet.id, currency: wallet.currency, balanceMinorUnits: walletBalance(this.db, walletId) }
  }

  private postTransaction(
    idempotencyIndexKey: string,
    type: Transaction['type'],
    entries: Transaction['entries'],
    reversalOfTransactionId: string | null = null,
  ): Transaction {
    const existingId = this.db.idempotencyIndex[idempotencyIndexKey]
    if (existingId) {
      const existing = this.db.transactions.find((t) => t.id === existingId)
      if (existing) {
        // Same-key replay must match the original request's parameters exactly - a
        // different type/entries/reversal target under a reused key is a conflict, never
        // silently applied to the original result. Matches the real backend's
        // MatchesThisTransfer/MatchesThisFunding/MatchesThisReversal checks (see
        // TransferHandler.cs, SimulateFundingHandler.cs, ReverseTransactionHandler.cs) -
        // found missing here by independent review, not exercised by the current UI
        // (every call site generates a fresh crypto.randomUUID() key), but a real
        // divergence from the documented idempotency contract in MEMORY.md otherwise.
        const matches =
          existing.type === type &&
          existing.reversalOfTransactionId === reversalOfTransactionId &&
          existing.entries.length === entries.length &&
          entries.every((e) =>
            existing.entries.some(
              (ee) =>
                ee.accountId === e.accountId &&
                ee.direction === e.direction &&
                ee.amountMinorUnits === e.amountMinorUnits &&
                ee.currency === e.currency,
            ),
          )
        if (!matches) {
          throw new ApiError(
            'This idempotency key was already used with different parameters.',
            409,
            'idempotency_conflict',
          )
        }
        return existing
      }
    }

    const debits = entries
      .filter((e) => e.direction === 'Debit')
      .reduce((sum, e) => sum + e.amountMinorUnits, 0)
    const credits = entries
      .filter((e) => e.direction === 'Credit')
      .reduce((sum, e) => sum + e.amountMinorUnits, 0)
    if (debits !== credits) {
      throw new ApiError('Transaction does not balance.', 500, 'unbalanced_transaction')
    }

    const tx: Transaction = {
      id: id('tx'),
      postedAtUtc: new Date().toISOString(),
      type,
      reversalOfTransactionId,
      entries,
    }
    this.db.transactions.push(tx)
    this.db.idempotencyIndex[idempotencyIndexKey] = tx.id
    saveDb(this.db)
    return tx
  }

  async fundWallet(
    walletId: string,
    amountMinorUnits: number,
    idempotencyKey: string,
  ): Promise<Transaction> {
    await delay()
    const userId = this.requireAuth()
    const wallet = this.db.wallets.find((w) => w.id === walletId && w.ownerUserId === userId)
    if (!wallet) throw new ApiError('Wallet not found.', 404, 'not_found')
    if (amountMinorUnits <= 0) {
      throw new ApiError('Amount must be greater than zero.', 400, 'invalid_amount')
    }
    return this.postTransaction(`${userId}:${idempotencyKey}`, 'SimulatedFunding', [
      {
        accountId: wallet.id,
        direction: 'Debit',
        amountMinorUnits,
        currency: wallet.currency,
      },
      {
        accountId: SYSTEM_FUNDING_ACCOUNT_ID,
        direction: 'Credit',
        amountMinorUnits,
        currency: wallet.currency,
      },
    ])
  }

  async transfer(input: TransferInput): Promise<Transaction> {
    await delay(550)
    const userId = this.requireAuth()
    const from = this.db.wallets.find((w) => w.id === input.sourceWalletId && w.ownerUserId === userId)
    const to = this.db.wallets.find((w) => w.id === input.destinationWalletId)
    // Source not-found/not-yours is 404 (ownership leak protection); a bad destination
    // is a distinct 400 - mirrors WalletsController.Transfer's doc comment.
    if (!from) throw new ApiError('Source wallet not found.', 404, 'not_found')
    if (!to) {
      throw new ApiError('Destination wallet ID does not exist.', 400, 'destination_not_found')
    }
    if (from.id === to.id) {
      throw new ApiError('Cannot transfer a wallet to itself.', 400, 'same_wallet')
    }
    if (from.currency !== to.currency) {
      throw new ApiError(
        `Cross-currency transfer (${from.currency} -> ${to.currency}) is out of scope for this demo.`,
        400,
        'currency_mismatch',
      )
    }
    if (input.amountMinorUnits <= 0) {
      throw new ApiError('Amount must be greater than zero.', 400, 'invalid_amount')
    }
    const indexKey = `${userId}:${input.idempotencyKey}`
    if (!this.db.idempotencyIndex[indexKey]) {
      const available = walletBalance(this.db, from.id)
      if (available < input.amountMinorUnits) {
        throw new ApiError('Insufficient funds.', 422, 'insufficient_funds')
      }
    }
    // Debit-normal: money leaving the source is a Credit, money arriving at the
    // destination is a Debit (matches the fixed backend TransferHandler - see MEMORY.md).
    return this.postTransaction(indexKey, 'Transfer', [
      {
        accountId: from.id,
        direction: 'Credit',
        amountMinorUnits: input.amountMinorUnits,
        currency: from.currency,
      },
      {
        accountId: to.id,
        direction: 'Debit',
        amountMinorUnits: input.amountMinorUnits,
        currency: to.currency,
      },
    ])
  }

  async reverseTransaction(
    walletId: string,
    transactionId: string,
    idempotencyKey: string,
  ): Promise<Transaction> {
    await delay()
    const userId = this.requireAuth()
    const wallet = this.db.wallets.find((w) => w.id === walletId && w.ownerUserId === userId)
    if (!wallet) throw new ApiError('Wallet not found.', 404, 'not_found')

    const original = this.db.transactions.find(
      (t) => t.id === transactionId && t.entries.some((e) => e.accountId === walletId),
    )
    if (!original) throw new ApiError('Transaction not found.', 404, 'not_found')
    if (original.type === 'Reversal') {
      throw new ApiError('Cannot reverse a reversal transaction.', 400, 'reversal_of_reversal')
    }

    // Same authorization rule as the real backend's ReverseTransactionHandler (no admin
    // concept in this demo, so this mirrors the non-admin path only): self-service reversal
    // only works when it debits the CALLER's own wallet - see MEMORY.md "Reversal authorization".
    const debitedEntry = original.entries.find((e) => e.direction === 'Debit')
    if (!debitedEntry) throw new ApiError('Malformed transaction.', 500, 'no_debit_entry')
    const affectedWallet = this.db.wallets.find((w) => w.id === debitedEntry.accountId)
    if (!affectedWallet || affectedWallet.ownerUserId !== userId) {
      throw new ApiError('Transaction not found.', 404, 'not_found')
    }

    const indexKey = `${userId}:${idempotencyKey}`
    if (!this.db.idempotencyIndex[indexKey]) {
      const alreadyReversed = this.db.transactions.some(
        (t) => t.type === 'Reversal' && t.reversalOfTransactionId === original.id,
      )
      if (alreadyReversed) {
        throw new ApiError('This transaction has already been reversed.', 409, 'already_reversed')
      }
      const available = walletBalance(this.db, debitedEntry.accountId)
      if (available < debitedEntry.amountMinorUnits) {
        throw new ApiError('Insufficient funds to reverse this transaction.', 422, 'insufficient_funds')
      }
    }

    const mirror = (direction: Transaction['entries'][number]['direction']) =>
      direction === 'Debit' ? 'Credit' : 'Debit'

    return this.postTransaction(
      indexKey,
      'Reversal',
      original.entries.map((e) => ({ ...e, direction: mirror(e.direction) })),
      original.id,
    )
  }

  async getWalletReconciliation(walletId: string): Promise<ReconciliationReport> {
    await delay()
    const userId = this.requireAuth()
    const wallet = this.db.wallets.find((w) => w.id === walletId && w.ownerUserId === userId)
    if (!wallet) throw new ApiError('Wallet not found.', 404, 'not_found')

    // Independently recomputed from raw entries, same as the real backend's
    // ReconciliationEngine - walletBalance() IS that computation here (this demo has no
    // separate cached projection to compare against), so drift is always 0 by construction;
    // the shape still matches the real report so a future divergence would show correctly.
    const recomputed = walletBalance(this.db, walletId)
    const account: AccountReconciliation = {
      accountId: wallet.id,
      accountType: 'UserWallet',
      currency: wallet.currency,
      projectedBalanceMinorUnits: recomputed,
      recomputedBalanceMinorUnits: recomputed,
      driftMinorUnits: 0,
      isBalanced: true,
    }

    const touching = this.db.transactions.filter((t) => t.entries.some((e) => e.accountId === walletId))
    const unbalancedTransactions: UnbalancedTransaction[] = []
    for (const t of touching) {
      const byCurrency = new Map<Currency, { debits: number; credits: number }>()
      for (const e of t.entries) {
        const bucket = byCurrency.get(e.currency) ?? { debits: 0, credits: 0 }
        if (e.direction === 'Debit') bucket.debits += e.amountMinorUnits
        else bucket.credits += e.amountMinorUnits
        byCurrency.set(e.currency, bucket)
      }
      for (const [currency, { debits, credits }] of byCurrency) {
        if (debits !== credits) {
          unbalancedTransactions.push({
            transactionId: t.id,
            currency,
            totalDebitMinorUnits: debits,
            totalCreditMinorUnits: credits,
          })
        }
      }
    }

    return {
      generatedAtUtc: new Date().toISOString(),
      accounts: [account],
      unbalancedTransactions,
      isClean: account.isBalanced && unbalancedTransactions.length === 0,
    }
  }

  async getHistory(walletId: string, page = 1, pageSize = 20): Promise<PagedResult<Transaction>> {
    await delay()
    const userId = this.requireAuth()
    const wallet = this.db.wallets.find((w) => w.id === walletId && w.ownerUserId === userId)
    if (!wallet) throw new ApiError('Wallet not found.', 404, 'not_found')

    const related = this.db.transactions
      .filter((t) => t.entries.some((e) => e.accountId === walletId))
      .sort((a, b) => b.postedAtUtc.localeCompare(a.postedAtUtc))

    const start = (page - 1) * pageSize
    return {
      items: related.slice(start, start + pageSize),
      page,
      pageSize,
      totalCount: related.length,
    }
  }
}
