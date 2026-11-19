import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { isApiError, useAuth } from '../../auth/AuthContext'
import { Button } from '../../components/Button'
import { ErrorBanner } from '../../components/ErrorBanner'
import { Field } from '../../components/Field'
import { AuthLayout } from './AuthLayout'
import { useDevicePanel, wait } from './useDevicePanel'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const device = useDevicePanel()

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)
    device.setStatus('verifying')
    try {
      await login(email, password)
      device.setStatus('unlocked')
      await wait(950)
      const from = (location.state as { from?: string } | null)?.from ?? '/'
      navigate(from, { replace: true })
    } catch (err) {
      device.setStatus('denied')
      setError(isApiError(err) ? err.message : 'Something went wrong. Please try again.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthLayout
      title="Log in"
      onSubmit={handleSubmit}
      deviceStatus={device.status}
      deviceTyped={device.typed}
      devicePress={device.press}
      footer={
        <>
          No account? <Link to="/register">Create one</Link>
        </>
      }
    >
      {error && <ErrorBanner message={error} />}
      <Field
        label="Email"
        type="email"
        autoComplete="email"
        required
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        disabled={isSubmitting}
      />
      <Field
        label="Password"
        type="password"
        autoComplete="current-password"
        required
        value={password}
        onChange={(e) => {
          setPassword(e.target.value)
          device.onPasswordChange(e.target.value.length)
        }}
        disabled={isSubmitting}
      />
      <Button type="submit" variant="primary" isLoading={isSubmitting}>
        Log in
      </Button>
    </AuthLayout>
  )
}
