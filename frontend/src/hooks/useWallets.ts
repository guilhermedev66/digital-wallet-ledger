import { useCallback, useEffect, useState } from 'react'
import { apiClient, isApiError, type WalletWithBalance } from '../api'

type Status = 'loading' | 'success' | 'error'

// entryCount/lastActivityAtUtc are real values from GET history's totalCount and the
// newest item's postedAtUtc (page 1, size 1) - never fabricated, just not part of the
// plain WalletDto/balance shape, so composed here alongside the balance fetch.
export interface WalletSummary extends WalletWithBalance {
  entryCount: number
  lastActivityAtUtc: string | null
}

export function useWallets() {
  const [wallets, setWallets] = useState<WalletSummary[]>([])
  const [status, setStatus] = useState<Status>('loading')
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    setStatus('loading')
    setError(null)
    try {
      const list = await apiClient.listWallets()
      // WalletDto carries no balance (it's derived, never stored) - fetch each wallet's
      // balance and activity telemetry separately and compose the view the UI needs.
      const withBalances = await Promise.all(
        list.map(async (wallet) => {
          const [balance, history] = await Promise.all([
            apiClient.getWalletBalance(wallet.id),
            apiClient.getHistory(wallet.id, 1, 1),
          ])
          return {
            ...wallet,
            balanceMinorUnits: balance.balanceMinorUnits,
            entryCount: history.totalCount,
            lastActivityAtUtc: history.items[0]?.postedAtUtc ?? null,
          }
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
