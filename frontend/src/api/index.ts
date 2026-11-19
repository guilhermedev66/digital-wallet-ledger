import { HttpApiClient } from './httpClient'
import { MockApiClient } from './mockClient'
import { ApiError, type ApiClient } from './types'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined
const useMock = import.meta.env.VITE_USE_MOCK_API === 'true' || !apiBaseUrl

const mockClient = useMock ? new MockApiClient() : null

export const apiClient: ApiClient = mockClient ?? new HttpApiClient(apiBaseUrl!)

/** Demo-data loader. Exists ONLY when the app runs on the mock client; null with the real API. */
export const demoData = mockClient ? { load: () => mockClient.loadDemoData() } : null

export const DEMO_WALLET_PREFIX = 'Demo · '

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}

export * from './types'
