import { useEffect, useState } from 'react'
import { apiClient, type Transaction } from '../api'
import type { WalletSummary } from './useWallets'
import { describeTx } from '../lib/txView'

const DAY_MS = 24 * 60 * 60 * 1000
const PAGE_SIZE = 50
const MAX_PAGES = 6
export const SERIES_DAYS = 30

export interface BalancePoint {
  label: string
  minorUnits: number
}

/**
 * Loads a wallet's recent history (enough to cover the last 30 days) and derives:
 * - the newest transactions, and
 * - an end-of-day balance series walked backwards from the real current balance
 *   using each transaction's signed effect on this wallet. Single wallet, single currency.
 */
export function useWalletHistory(wallet: WalletSummary) {
  const [items, setItems] = useState<Transaction[] | null>(null)
  const [failed, setFailed] = useState(false)
  const trigger = `${wallet.id}:${wallet.entryCount}:${wallet.balanceMinorUnits}`

  useEffect(() => {
    let cancelled = false
    const cutoff = Date.now() - (SERIES_DAYS + 1) * DAY_MS
    async function run() {
      const all: Transaction[] = []
      for (let page = 1; page <= MAX_PAGES; page++) {
        const result = await apiClient.getHistory(wallet.id, page, PAGE_SIZE)
        all.push(...result.items)
        const oldest = result.items[result.items.length - 1]
        if (all.length >= result.totalCount || !oldest || Date.parse(oldest.postedAtUtc) < cutoff) break
      }
      return all
    }
    run()
      .then((all) => {
        if (cancelled) return
        setItems(all)
        setFailed(false)
      })
      .catch(() => {
        if (!cancelled) setFailed(true)
      })
    return () => {
      cancelled = true
    }
  }, [trigger, wallet.id])

  let series: BalancePoint[] = []
  if (items) {
    const startOfToday = new Date()
    startOfToday.setHours(0, 0, 0, 0)
    const fmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' })
    for (let back = SERIES_DAYS - 1; back >= 0; back--) {
      const dayStart = startOfToday.getTime() - back * DAY_MS
      const dayEnd = dayStart + DAY_MS
      // Balance at end of this day = current balance minus everything posted after it.
      const after = items
        .filter((tx) => Date.parse(tx.postedAtUtc) >= dayEnd)
        .reduce((sum, tx) => sum + describeTx(tx, wallet.id).signedMinorUnits, 0)
      series.push({ label: fmt.format(dayStart), minorUnits: wallet.balanceMinorUnits - after })
    }
    // Wallet didn't exist before its first entry: drop the leading flat zeros except one.
    const firstNonZero = series.findIndex((p) => p.minorUnits !== 0)
    if (firstNonZero > 0) series = series.slice(Math.max(0, firstNonZero - 1))
  }

  return { items, failed, series }
}
