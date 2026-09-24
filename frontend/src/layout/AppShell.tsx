import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { Button } from '../components/Button'
import { CommandPalette } from '../components/CommandPalette'
import { Kbd } from '../components/Kbd'
import { ThemeToggle } from '../components/ThemeToggle'
import styles from './AppShell.module.css'

const NAV_ITEMS = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/transfer', label: 'Transfer', end: false },
  { to: '/activity', label: 'Activity', end: false },
  { to: '/reconciliation', label: 'Reconciliation', end: false },
]

export function AppShell() {
  const { user, logout } = useAuth()
  const [isPaletteOpen, setIsPaletteOpen] = useState(false)
  const paletteButtonRef = useRef<HTMLButtonElement>(null)
  const paletteOpenerRef = useRef<HTMLElement | null>(null)

  function openPalette() {
    paletteOpenerRef.current = document.activeElement as HTMLElement | null
    setIsPaletteOpen(true)
  }

  function closePalette() {
    setIsPaletteOpen(false)
    // Return focus to whatever opened the dialog (the button, or the element that had
    // focus when ⌘K fired) instead of letting it fall back to <body>.
    const opener = paletteOpenerRef.current
    if (opener && document.contains(opener)) opener.focus()
    else paletteButtonRef.current?.focus()
  }

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        openPalette()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [])

  return (
    <div className={styles.shell}>
      <a className="skip-link" href="#main">
        Skip to main content
      </a>
      <header className={styles.header}>
        <div className={styles.headerInner}>
          <div className={styles.identity}>
            <span className={styles.brand}>Wallet &amp; Ledger</span>
            <span className={styles.statusDot} aria-hidden="true" />
            <span className={styles.terminalBadge + ' mono'}>TERMINAL</span>
          </div>
          <nav className={styles.nav} aria-label="Primary">
            {NAV_ITEMS.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                end={item.end}
                className={({ isActive }) =>
                  [styles.navLink, isActive ? styles.navLinkActive : ''].join(' ')
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>
          <div className={styles.headerActions}>
            <button
              ref={paletteButtonRef}
              type="button"
              className={styles.paletteButton}
              onClick={openPalette}
            >
              <span>Search</span>
              <Kbd keys={['cmd', 'k']} />
            </button>
            <ThemeToggle />
            <span className={styles.userEmail}>{user?.email}</span>
            <Button variant="secondary" onClick={logout}>
              Log out
            </Button>
          </div>
        </div>
      </header>
      <main id="main" className={styles.main}>
        <Outlet />
      </main>
      <CommandPalette isOpen={isPaletteOpen} onClose={closePalette} />
    </div>
  )
}
