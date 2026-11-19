import { lazy, Suspense, useEffect, useRef, useState, type FormEvent } from 'react'
import { LockKeyhole } from 'lucide-react'
import { apiClient, isApiError } from '../api'
import { useDevicePanel, wait } from '../pages/auth/useDevicePanel'
import { Button } from './Button'
import { Field } from './Field'
import styles from './LockScreen.module.css'

const WalletDevice = lazy(() => import('../pages/auth/WalletDevice'))

/**
 * Full-screen lock. Unlocking re-verifies the password through the same API login the sign-in
 * form uses (current e-mail). It is a convenience lock, not a security boundary: the session
 * token stays in storage; a wrong password never ends the session.
 */
export function LockScreen({ email, onUnlock }: { email: string; onUnlock: () => void }) {
  const device = useDevicePanel()
  const [password, setPassword] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const formRef = useRef<HTMLFormElement>(null)

  useEffect(() => {
    formRef.current?.querySelector('input')?.focus()
  }, [])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!password || busy) return
    setBusy(true)
    setError(null)
    device.setStatus('verifying')
    try {
      await apiClient.login(email, password)
      device.setStatus('unlocked')
      await wait(950)
      onUnlock()
    } catch (err) {
      device.setStatus('denied')
      setError(isApiError(err) && err.status === 401 ? 'Wrong password.' : 'Could not verify. Please try again.')
      setPassword('')
      setBusy(false)
      requestAnimationFrame(() => formRef.current?.querySelector('input')?.focus())
    }
  }

  return (
    <div className={styles.overlay} role="dialog" aria-modal="true" aria-label="Screen locked">
      <div className={styles.device}>
        <Suspense fallback={null}>
          <WalletDevice status={device.status} typed={device.typed} press={device.press} large />
        </Suspense>
      </div>
      <form ref={formRef} className={styles.form} onSubmit={submit} noValidate>
        <p className={styles.lockTitle}>
          <LockKeyhole size={18} aria-hidden="true" /> Screen locked
        </p>
        <p className={styles.email + ' mono'}>{email}</p>
        {error && (
          <p className={styles.error} role="alert">
            {error}
          </p>
        )}
        <Field
          label="Password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => {
            setPassword(e.target.value)
            device.onPasswordChange(e.target.value.length)
          }}
          disabled={busy}
        />
        <Button type="submit" variant="primary" isLoading={busy}>
          Unlock
        </Button>
      </form>
    </div>
  )
}
