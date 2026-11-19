import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { motion, useReducedMotion } from 'motion/react'
import {
  ArrowLeftRight,
  ChevronDown,
  History,
  LayoutDashboard,
  LockKeyhole,
  LogOut,
  Moon,
  Sparkles,
  Sun,
  Scale,
  Search,
} from 'lucide-react'
import { demoData } from '../api'
import { useAuth } from '../auth/AuthContext'
import { Brand } from '../components/Brand'
import { CommandPalette } from '../components/CommandPalette'
import { Kbd } from '../components/Kbd'
import { LockScreen } from '../components/LockScreen'
import { useTheme } from '../hooks/useTheme'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '../components/ui/dropdown-menu'
import styles from './AppShell.module.css'

const NAV_SECTIONS = [
  { title: 'Overview', items: [{ to: '/', label: 'Dashboard', end: true, Icon: LayoutDashboard }] },
  {
    title: 'Money',
    items: [
      { to: '/transfer', label: 'Transfer', end: false, Icon: ArrowLeftRight },
      { to: '/activity', label: 'Activity', end: false, Icon: History },
    ],
  },
  { title: 'Audit', items: [{ to: '/reconciliation', label: 'Reconciliation', end: false, Icon: Scale }] },
]

const LOCK_KEY = 'wallet-ledger-locked'

export function AppShell() {
  const { user, logout } = useAuth()
  const reduceMotion = useReducedMotion()
  const location = useLocation()
  const [isLoadingDemo, setIsLoadingDemo] = useState(false)
  const { theme, toggle: toggleTheme } = useTheme()
  const [locked, setLocked] = useState(() => {
    try {
      return sessionStorage.getItem(LOCK_KEY) === '1'
    } catch {
      return false
    }
  })

  function setLock(next: boolean) {
    setLocked(next)
    try {
      if (next) sessionStorage.setItem(LOCK_KEY, '1')
      else sessionStorage.removeItem(LOCK_KEY)
    } catch {
      // best-effort persistence of the lock across reloads
    }
  }
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

  const initial = (user?.email?.[0] ?? '?').toUpperCase()

  return (
    <>
    <div className={styles.shell} inert={locked || undefined}>
      <a className="skip-link" href="#main">
        Skip to main content
      </a>
      <aside className={styles.sidebar}>
        <div className={styles.brandRow}>
          <span className={styles.brandFull}>
            <Brand />
          </span>
          <span className={styles.brandCompact}>
            <Brand showText={false} />
          </span>
        </div>
        <nav className={styles.nav} aria-label="Primary">
          {NAV_SECTIONS.map((section) => (
            <div key={section.title} className={styles.section} role="group" aria-label={section.title}>
              <p className={styles.sectionTitle} aria-hidden="true">
                {section.title}
              </p>
              {section.items.map(({ to, label, end, Icon }) => (
                <NavLink
                  key={to}
                  to={to}
                  end={end}
                  aria-label={label}
                  className={({ isActive }) =>
                    [styles.navLink, isActive ? styles.navLinkActive : ''].join(' ')
                  }
                >
                  {({ isActive }) => (
                    <>
                      {isActive && (
                        <motion.span
                          layoutId="nav-indicator"
                          className={styles.indicator}
                          aria-hidden="true"
                          transition={
                            reduceMotion
                              ? { duration: 0 }
                              : { type: 'spring', stiffness: 520, damping: 40 }
                          }
                        />
                      )}
                      <Icon size={18} aria-hidden="true" className={styles.navIcon} />
                      <span className={styles.navLabel}>{label}</span>
                    </>
                  )}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
      </aside>
      <div className={styles.column}>
        <header className={styles.header}>
          <button
            ref={paletteButtonRef}
            type="button"
            className={styles.paletteButton}
            onClick={openPalette}
          >
            <Search size={15} aria-hidden="true" />
            <span>Search</span>
            <Kbd keys={['cmd', 'k']} />
          </button>
          <div className={styles.headerActions}>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button type="button" className={styles.userButton} aria-label="User menu">
                  <span className={styles.avatar} aria-hidden="true">
                    {initial}
                  </span>
                  <span className={styles.userEmail}>{user?.email}</span>
                  <ChevronDown size={14} aria-hidden="true" />
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="min-w-52">
                <DropdownMenuLabel className="font-mono text-xs break-all">
                  {user?.email}
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem onSelect={toggleTheme}>
                  {theme === 'dark' ? <Sun aria-hidden="true" /> : <Moon aria-hidden="true" />}
                  {theme === 'dark' ? 'Light theme' : 'Dark theme'}
                </DropdownMenuItem>
                {demoData && (
                  <DropdownMenuItem
                    disabled={isLoadingDemo}
                    onSelect={async () => {
                      setIsLoadingDemo(true)
                      await demoData?.load()
                      window.location.assign('/')
                    }}
                  >
                    <Sparkles aria-hidden="true" />
                    {isLoadingDemo ? 'Loading demo data…' : 'Load demo data (mock)'}
                  </DropdownMenuItem>
                )}
                <DropdownMenuItem onSelect={() => setLock(true)}>
                  <LockKeyhole aria-hidden="true" />
                  Lock screen
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem onSelect={() => logout()}>
                  <LogOut aria-hidden="true" />
                  Log out
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </header>
        <main id="main" className={styles.main}>
          <motion.div
            key={location.pathname}
            initial={reduceMotion ? { opacity: 0 } : { opacity: 0, y: 14 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.38, ease: [0.22, 1, 0.36, 1] }}
          >
            <Outlet />
          </motion.div>
        </main>
      </div>
      <CommandPalette isOpen={isPaletteOpen && !locked} onClose={closePalette} />
    </div>
    {locked && user && <LockScreen email={user.email} onUnlock={() => setLock(false)} />}
    </>
  )
}
