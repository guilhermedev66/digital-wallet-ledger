import { useEffect, useState } from 'react'
import { apiClient, type Transaction } from '../api'
import type { WalletSummary } from './useWallets'

const DAY_MS = 24 * 60 * 60 * 1000
const PAGE_SIZE = 50
const MAX_PAGES = 6
/** Window covered by the dashboard stats (30 days + a day of slack). */
export const PORTFOLIO_DAYS = 30

/**
 * All of the user's recent transactions across wallets, de-duplicated by id (a transfer between two
 * own wallets appears in both histories). Used for counts and single-currency aggregates only.
 */
export function usePortfolioHistory(wallets: WalletSummary[]) {
  const [txs, setTxs] = useState<Transaction[] | null>(null)
  const [failed, setFailed] = useState(false)
  const trigger = wallets.map((w) => `${w.id}:${w.entryCount}`).join('|')

  useEffect(() => {
    let cancelled = false
    const cutoff = Date.now() - (PORTFOLIO_DAYS + 1) * DAY_MS
    const ids = trigger ? trigger.split('|').map((part) => part.split(':')[0]) : []

    async function forWallet(id: string) {
      const all: Transaction[] = []
      for (let page = 1; page <= MAX_PAGES; page++) {
        const result = await apiClient.getHistory(id, page, PAGE_SIZE)
        all.push(...result.items)
        const oldest = result.items[result.items.length - 1]
        if (all.length >= result.totalCount || !oldest || Date.parse(oldest.postedAtUtc) < cutoff) break
      }
      return all
    }

    Promise.all(ids.map(forWallet))
      .then((lists) => {
        if (cancelled) return
        const byId = new Map<string, Transaction>()
        for (const tx of lists.flat()) byId.set(tx.id, tx)
        setTxs([...byId.values()])
        setFailed(false)
      })
      .catch(() => {
        if (!cancelled) setFailed(true)
      })
    return () => {
      cancelled = true
    }
  }, [trigger])

  return { txs, failed }
}
