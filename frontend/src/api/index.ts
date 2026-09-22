import { HttpApiClient } from './httpClient'
import { MockApiClient } from './mockClient'
import { ApiError, type ApiClient } from './types'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined
const useMock = import.meta.env.VITE_USE_MOCK_API === 'true' || !apiBaseUrl

export const apiClient: ApiClient = useMock
  ? new MockApiClient()
  : new HttpApiClient(apiBaseUrl!)

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}

export * from './types'
