export type Currency = 'USD' | 'BRL'

// 'Reversal' will be added once M4 lands and the backend TransactionDto actually returns it.
export type TransactionType = 'Transfer' | 'SimulatedFunding'

export type EntryDirection = 'Debit' | 'Credit'

export interface User {
  id: string
  email: string
}

// Mirrors WalletDto exactly - the backend has no mutable balance column, so a Wallet
// alone never carries a balance. See WalletWithBalance for the composed view the UI uses.
export interface Wallet {
  id: string
  ownerUserId: string
  currency: Currency
  displayName: string | null
  createdAtUtc: string
}

export interface WalletWithBalance extends Wallet {
  balanceMinorUnits: number
}

export interface WalletBalance {
  walletId: string
  currency: Currency
  balanceMinorUnits: number
}

export interface LedgerEntry {
  accountId: string
  direction: EntryDirection
  amountMinorUnits: number
  currency: Currency
}

// Mirrors TransactionDto - posting is atomic/synchronous (see ARCHITECTURE.md), so a
// response is always already posted; there is no server-side "Pending" status to model.
export interface Transaction {
  id: string
  type: TransactionType
  postedAtUtc: string
  entries: LedgerEntry[]
}

export interface AuthSession {
  user: User
  token: string
}

export interface RegisteredUser {
  userId: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export class ApiError extends Error {
  readonly status: number
  readonly code?: string

  constructor(message: string, status: number, code?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

export interface TransferInput {
  sourceWalletId: string
  destinationWalletId: string
  amountMinorUnits: number
  idempotencyKey: string
}

export interface ApiClient {
  setAuthToken(token: string | null): void

  register(email: string, password: string): Promise<RegisteredUser>
  login(email: string, password: string): Promise<AuthSession>

  listWallets(): Promise<Wallet[]>
  createWallet(currency: Currency, displayName?: string): Promise<Wallet>
  getWallet(walletId: string): Promise<Wallet>
  getWalletBalance(walletId: string): Promise<WalletBalance>

  fundWallet(
    walletId: string,
    amountMinorUnits: number,
    idempotencyKey: string,
  ): Promise<Transaction>

  transfer(input: TransferInput): Promise<Transaction>

  getHistory(walletId: string, page?: number, pageSize?: number): Promise<PagedResult<Transaction>>
}
