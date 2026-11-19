import {
  AnimatePresence,
  motion,
  useMotionTemplate,
  useMotionValue,
  useReducedMotion,
  useSpring,
  useTransform,
} from 'motion/react'
import type { PointerEvent } from 'react'
import type { DeviceStatus, KeyPress } from './deviceTypes'
import styles from './WalletDevice.module.css'

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '⌫', '0', '↵']

function displayText(status: DeviceStatus, typed: number): string {
  if (status === 'verifying') return 'VERIFYING…'
  if (status === 'unlocked') return 'UNLOCKED'
  if (status === 'denied') return 'DENIED'
  return typed > 0 ? '•'.repeat(Math.min(typed, 14)) : 'READY'
}

/**
 * Original decorative hardware-wallet keypad (CSS + motion). Big, layered 3D object:
 * mouse tilt, a cursor-following specular glare, raised keys, and a mint light flash on
 * success. aria-hidden: the form beside it already announces every state.
 */
export default function WalletDevice({
  status,
  typed,
  press,
  large = false,
}: {
  status: DeviceStatus
  typed: number
  press: KeyPress | null
  large?: boolean
}) {
  const reduce = useReducedMotion()
  const px = useMotionValue(0.5)
  const py = useMotionValue(0.5)
  const rotateY = useSpring(useTransform(px, [0, 1], [-16, 16]), { stiffness: 120, damping: 16 })
  const rotateX = useSpring(useTransform(py, [0, 1], [14, -14]), { stiffness: 120, damping: 16 })
  const gx = useTransform(px, (v) => v * 100)
  const gy = useTransform(py, (v) => v * 100)
  const glare = useMotionTemplate`radial-gradient(circle at ${gx}% ${gy}%, rgba(255,255,255,0.42) 0%, rgba(190,170,255,0.16) 28%, transparent 55%)`

  function onPointerMove(event: PointerEvent<HTMLDivElement>) {
    if (reduce) return
    const rect = event.currentTarget.getBoundingClientRect()
    px.set((event.clientX - rect.left) / rect.width)
    py.set((event.clientY - rect.top) / rect.height)
  }

  function onPointerLeave() {
    px.set(0.5)
    py.set(0.5)
  }

  return (
    <div className={[styles.stage, large ? styles.large : ''].join(' ')} aria-hidden="true" onPointerMove={onPointerMove} onPointerLeave={onPointerLeave}>
      <div className={styles.halo} />
      <motion.div
        className={styles.float}
        animate={reduce ? undefined : { y: [0, -10, 0] }}
        transition={{ duration: 6, repeat: Infinity, ease: 'easeInOut' }}
      >
        <motion.div
          className={styles.device}
          style={reduce ? undefined : { rotateX, rotateY, transformPerspective: 1200 }}
          initial={reduce ? false : { opacity: 0, scale: 0.9, y: 30 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          transition={{ duration: 0.7, ease: [0.22, 1, 0.36, 1] }}
        >
          <span className={[styles.screw, styles.s1].join(' ')} />
          <span className={[styles.screw, styles.s2].join(' ')} />
          <span className={[styles.screw, styles.s3].join(' ')} />
          <span className={[styles.screw, styles.s4].join(' ')} />

          <div className={styles.topRow}>
            <div
              className={[
                styles.display,
                status === 'denied' ? styles.denied : '',
                status === 'unlocked' ? styles.unlocked : '',
              ].join(' ')}
            >
              <span key={status} className={styles.displayText}>
                {displayText(status, typed)}
              </span>
            </div>
            <div className={styles.leds}>
              <span className={status === 'verifying' ? styles.ledBusy : ''} />
              <span />
              <span />
            </div>
          </div>

          <div className={styles.keys}>
            {KEYS.map((label, index) => {
              const flashing = press?.key === index
              return (
                <span
                  // Re-keying on press.id restarts the CSS flash animation.
                  key={flashing ? `${index}-${press.id}` : index}
                  className={[styles.key, flashing ? styles.flash : ''].join(' ')}
                >
                  {label}
                </span>
              )
            })}
          </div>

          <div className={styles.footer}>
            <span>WALLET &amp; LEDGER · SIM</span>
            <span className={styles.pins}>
              {Array.from({ length: 7 }).map((_, i) => (
                <i key={i} />
              ))}
            </span>
          </div>

          <motion.span className={styles.glare} style={reduce ? undefined : { background: glare }} />
        </motion.div>
      </motion.div>

      <AnimatePresence>
        {status === 'unlocked' && (
          <motion.span
            key="flash"
            className={styles.flashLight}
            initial={{ opacity: 0 }}
            animate={{ opacity: reduce ? [0, 0.5, 0] : [0, 0.95, 0] }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.85, ease: 'easeOut' }}
          />
        )}
      </AnimatePresence>
    </div>
  )
}
