import { useCallback, useEffect, useState } from 'react'
import { apiClient, isApiError, type WalletWithBalance } from '../api'

type Status = 'loading' | 'success' | 'error'

export function useWallets() {
  const [wallets, setWallets] = useState<WalletWithBalance[]>([])
  const [status, setStatus] = useState<Status>('loading')
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    setStatus('loading')
    setError(null)
    try {
      const list = await apiClient.listWallets()
      // WalletDto carries no balance (it's derived, never stored) - fetch each wallet's
      // balance separately, per GET /api/wallets/{id}/balance, and compose the view the UI needs.
      const withBalances = await Promise.all(
        list.map(async (wallet) => {
          const balance = await apiClient.getWalletBalance(wallet.id)
          return { ...wallet, balanceMinorUnits: balance.balanceMinorUnits }
        }),
      )
      setWallets(withBalances)
      setStatus('success')
    } catch (err) {
      setError(isApiError(err) ? err.message : 'Could not load wallets.')
      setStatus('error')
    }
  }, [])

  useEffect(() => {
    refresh()
  }, [refresh])

  return { wallets, status, error, refresh }
}
