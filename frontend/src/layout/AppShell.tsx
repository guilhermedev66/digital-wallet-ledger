import { useEffect, useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { Button } from '../components/Button'
import { CommandPalette } from '../components/CommandPalette'
import styles from './AppShell.module.css'

const NAV_ITEMS = [
  { to: '/', label: 'Dashboard', end: true },
  { to: '/transfer', label: 'Transfer', end: false },
  { to: '/activity', label: 'Activity', end: false },
]

export function AppShell() {
  const { user, logout } = useAuth()
  const [isPaletteOpen, setIsPaletteOpen] = useState(false)

  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault()
        setIsPaletteOpen(true)
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
          <span className={styles.brand}>Wallet &amp; Ledger</span>
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
              type="button"
              className={styles.paletteButton}
              onClick={() => setIsPaletteOpen(true)}
            >
              Search <span className="mono">&#8984;K</span>
            </button>
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
      <CommandPalette isOpen={isPaletteOpen} onClose={() => setIsPaletteOpen(false)} />
    </div>
  )
}
