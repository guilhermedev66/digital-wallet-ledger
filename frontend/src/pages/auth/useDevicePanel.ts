import { useCallback, useRef, useState } from 'react'
import { DEVICE_KEY_COUNT, type DeviceStatus, type KeyPress } from './deviceTypes'

/**
 * State that drives the decorative wallet device next to the auth forms.
 * Purely visual: it never touches credentials, only the password's length.
 */
export function useDevicePanel() {
  const [status, setStatus] = useState<DeviceStatus>('idle')
  const [typed, setTyped] = useState(0)
  const [press, setPress] = useState<KeyPress | null>(null)
  const counter = useRef(0)

  const onPasswordChange = useCallback((length: number) => {
    setStatus((current) => (current === 'denied' ? 'idle' : current))
    setTyped(length)
    counter.current += 1
    setPress({ id: counter.current, key: Math.floor(Math.random() * DEVICE_KEY_COUNT) })
  }, [])

  return { status, setStatus, typed, press, onPasswordChange }
}

export const wait = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms))
