import type { Currency } from '../api'

// Labels only - no FX/conversion anywhere in this app. Each wallet's balance
// stays in its own currency; balances are never summed across currencies.
export const CURRENCIES: ReadonlyArray<{ code: Currency; label: string }> = [
  { code: 'USD', label: 'US Dollar' },
  { code: 'BRL', label: 'Brazilian Real' },
  { code: 'EUR', label: 'Euro' },
  { code: 'GBP', label: 'British Pound' },
  { code: 'CHF', label: 'Swiss Franc' },
  { code: 'CAD', label: 'Canadian Dollar' },
  { code: 'AUD', label: 'Australian Dollar' },
]
