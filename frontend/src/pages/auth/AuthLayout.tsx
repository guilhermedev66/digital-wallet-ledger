import { lazy, Suspense, useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { Brand } from '../../components/Brand'
import { RevealTitle, RevealWords } from '../../components/RevealText'
import type { DeviceStatus, KeyPress } from './deviceTypes'
import styles from './AuthLayout.module.css'

const WalletDevice = lazy(() => import('./WalletDevice'))

const WIDE_QUERY = '(min-width: 900px)'

function useIsWide() {
  const [isWide, setIsWide] = useState(() => window.matchMedia(WIDE_QUERY).matches)
  useEffect(() => {
    const mql = window.matchMedia(WIDE_QUERY)
    const onChange = (event: MediaQueryListEvent) => setIsWide(event.matches)
    mql.addEventListener('change', onChange)
    return () => mql.removeEventListener('change', onChange)
  }, [])
  return isWide
}

export function AuthLayout({
  title,
  children,
  footer,
  onSubmit,
  deviceStatus = 'idle',
  deviceTyped = 0,
  devicePress = null,
}: {
  title: string
  children: ReactNode
  footer?: ReactNode
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  deviceStatus?: DeviceStatus
  deviceTyped?: number
  devicePress?: KeyPress | null
}) {
  const isWide = useIsWide()

  return (
    <div className={styles.page}>
      <div className={styles.formSide}>
        <div className={styles.panel}>
          <div className={styles.brand}>
            <Brand size={32} />
            <RevealWords className={styles.tagline} delay={0.5}>
              Simulated double-entry ledger. No real money, no real banks.
            </RevealWords>
          </div>
          <RevealTitle as="h1" className={styles.title}>
            {title}
          </RevealTitle>
          <form className={styles.form} onSubmit={onSubmit} noValidate>
            {children}
          </form>
          {footer && <div className={styles.footer}>{footer}</div>}
        </div>
      </div>
      {isWide && (
        <div className={styles.deviceSide}>
          <Suspense fallback={null}>
            <WalletDevice status={deviceStatus} typed={deviceTyped} press={devicePress} />
          </Suspense>
        </div>
      )}
    </div>
  )
}
