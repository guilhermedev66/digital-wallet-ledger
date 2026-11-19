import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { isApiError, useAuth } from '../../auth/AuthContext'
import { Button } from '../../components/Button'
import { ErrorBanner } from '../../components/ErrorBanner'
import { Field } from '../../components/Field'
import { AuthLayout } from './AuthLayout'
import { useDevicePanel, wait } from './useDevicePanel'

export function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const device = useDevicePanel()

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    if (password !== confirmPassword) {
      device.setStatus('denied')
      setError('Passwords do not match.')
      return
    }

    setIsSubmitting(true)
    device.setStatus('verifying')
    try {
      await register(email, password)
      device.setStatus('unlocked')
      await wait(950)
      navigate('/', { replace: true })
    } catch (err) {
      device.setStatus('denied')
      setError(isApiError(err) ? err.message : 'Something went wrong. Please try again.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthLayout
      title="Create account"
      onSubmit={handleSubmit}
      deviceStatus={device.status}
      deviceTyped={device.typed}
      devicePress={device.press}
      footer={
        <>
          Already have an account? <Link to="/login">Log in</Link>
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
        autoComplete="new-password"
        required
        minLength={8}
        hint="At least 8 characters."
        value={password}
        onChange={(e) => {
          setPassword(e.target.value)
          device.onPasswordChange(e.target.value.length)
        }}
        disabled={isSubmitting}
      />
      <Field
        label="Confirm password"
        type="password"
        autoComplete="new-password"
        required
        value={confirmPassword}
        onChange={(e) => setConfirmPassword(e.target.value)}
        disabled={isSubmitting}
      />
      <Button type="submit" variant="primary" isLoading={isSubmitting}>
        Create account
      </Button>
    </AuthLayout>
  )
}
