export type DeviceStatus = 'idle' | 'verifying' | 'unlocked' | 'denied'

/** A single key flash: `id` changes on every press so the key re-triggers its animation. */
export interface KeyPress {
  id: number
  key: number
}

export const DEVICE_KEY_COUNT = 12
