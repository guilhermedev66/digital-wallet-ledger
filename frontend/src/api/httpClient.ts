import type {
  ApiClient,
  AuthSession,
  Currency,
  PagedResult,
  ReconciliationReport,
  RegisteredUser,
  Transaction,
  TransferInput,
  Wallet,
  WalletBalance,
} from './types'
import { ApiError } from './types'

export class HttpApiClient implements ApiClient {
  private token: string | null = null
  private readonly baseUrl: string

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl
  }

  setAuthToken(token: string | null) {
    this.token = token
  }

  private async request<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      ...init,
      headers: {
        'Content-Type': 'application/json',
        ...(this.token ? { Authorization: `Bearer ${this.token}` } : {}),
        ...init?.headers,
      },
    })

    if (!response.ok) {
      const body = await response.json().catch(() => null)
      throw new ApiError(
        body?.message ?? `Request failed with status ${response.status}`,
        response.status,
        body?.code,
      )
    }

    if (response.status === 204) return undefined as T
    return (await response.json()) as T
  }

  async register(email: string, password: string): Promise<RegisteredUser> {
    return this.request<RegisteredUser>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
  }

  async login(email: string, password: string): Promise<AuthSession> {
    const result = await this.request<{
      userId: string
      email: string
      accessToken: string
      expiresAtUtc: string
    }>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    return { user: { id: result.userId, email: result.email }, token: result.accessToken }
  }

  listWallets() {
    return this.request<Wallet[]>('/api/wallets')
  }

  createWallet(currency: Currency, displayName?: string) {
    return this.request<Wallet>('/api/wallets', {
      method: 'POST',
      body: JSON.stringify({ currency, displayName }),
    })
  }

  getWallet(walletId: string) {
    return this.request<Wallet>(`/api/wallets/${walletId}`)
  }

  getWalletBalance(walletId: string) {
    return this.request<WalletBalance>(`/api/wallets/${walletId}/balance`)
  }

  fundWallet(walletId: string, amountMinorUnits: number, idempotencyKey: string) {
    return this.request<Transaction>(`/api/wallets/${walletId}/simulate-funding`, {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify({ amountMinorUnits }),
    })
  }

  transfer(input: TransferInput) {
    return this.request<Transaction>(`/api/wallets/${input.sourceWalletId}/transfer`, {
      method: 'POST',
      headers: { 'Idempotency-Key': input.idempotencyKey },
      body: JSON.stringify({
        destinationWalletId: input.destinationWalletId,
        amountMinorUnits: input.amountMinorUnits,
      }),
    })
  }

  getHistory(walletId: string, page = 1, pageSize = 20) {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
    return this.request<PagedResult<Transaction>>(`/api/wallets/${walletId}/history?${params}`)
  }

  reverseTransaction(walletId: string, transactionId: string, idempotencyKey: string) {
    return this.request<Transaction>(
      `/api/wallets/${walletId}/transactions/${transactionId}/reverse`,
      { method: 'POST', headers: { 'Idempotency-Key': idempotencyKey } },
    )
  }

  getWalletReconciliation(walletId: string) {
    return this.request<ReconciliationReport>(`/api/wallets/${walletId}/reconciliation`)
  }
}
