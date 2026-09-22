import type { Currency } from '../api'

export function formatAmount(amountMinorUnits: number, currency: Currency): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
    currencySign: 'standard',
  }).format(amountMinorUnits / 100)
}

export function formatSignedAmount(
  amountMinorUnits: number,
  currency: Currency,
  sign: 1 | -1,
): string {
  const formatted = formatAmount(Math.abs(amountMinorUnits), currency)
  return sign >= 0 ? `+${formatted}` : `-${formatted}`
}

export function parseAmountToMinorUnits(input: string): number | null {
  const trimmed = input.trim().replace(/,/g, '')
  if (!/^\d+(\.\d{1,2})?$/.test(trimmed)) return null
  const [wholePart, fractionPart = ''] = trimmed.split('.')
  const cents = (fractionPart + '00').slice(0, 2)
  return Number(wholePart) * 100 + Number(cents)
}
