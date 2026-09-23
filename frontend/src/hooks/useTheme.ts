import { useCallback, useEffect, useState } from 'react'

export type ThemePreference = 'light' | 'dark'

// Kept in sync with the inline pre-hydration script in index.html.
const STORAGE_KEY = 'wallet-ledger-theme'

function readStoredTheme(): ThemePreference | null {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    return stored === 'light' || stored === 'dark' ? stored : null
  } catch {
    return null
  }
}

function readSystemTheme(): ThemePreference {
  return window.matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark'
}

/**
 * Tracks the effective theme and lets the user pin an explicit choice.
 * Before any explicit choice, `data-theme` is left unset entirely so
 * tokens.css's `prefers-color-scheme` rules keep governing (including live
 * OS theme changes) - only a toggle click starts pinning the attribute.
 */
export function useTheme() {
  const [explicitTheme, setExplicitTheme] = useState<ThemePreference | null>(readStoredTheme)
  const [systemTheme, setSystemTheme] = useState<ThemePreference>(readSystemTheme)

  useEffect(() => {
    const mql = window.matchMedia('(prefers-color-scheme: light)')
    function handleChange(event: MediaQueryListEvent) {
      setSystemTheme(event.matches ? 'light' : 'dark')
    }
    mql.addEventListener('change', handleChange)
    return () => mql.removeEventListener('change', handleChange)
  }, [])

  useEffect(() => {
    if (explicitTheme) {
      document.documentElement.setAttribute('data-theme', explicitTheme)
    } else {
      document.documentElement.removeAttribute('data-theme')
    }
  }, [explicitTheme])

  const theme = explicitTheme ?? systemTheme

  const setTheme = useCallback((next: ThemePreference) => {
    setExplicitTheme(next)
    try {
      localStorage.setItem(STORAGE_KEY, next)
    } catch {
      // best-effort persistence only; theme still applies for this session
    }
  }, [])

  const toggle = useCallback(() => {
    setTheme(theme === 'dark' ? 'light' : 'dark')
  }, [theme, setTheme])

  return { theme, hasExplicitChoice: explicitTheme !== null, setTheme, toggle }
}
