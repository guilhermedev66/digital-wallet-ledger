import { useEffect, useState } from 'react'
import { animate, motion, useMotionValue, useReducedMotion } from 'motion/react'
import type { Currency } from '../api'
import styles from './AmountDisplay.module.css'

/**
 * Money with tabular figures. Cents are muted ("5,293.00"). The value rolls from the old
 * amount to the new one. With `flashOnIncrease`, a rise in the SAME wallet (`resetKey` unchanged)
 * flashes the figure mint - "money arrived". Renders exactly one amount; never sums currencies.
 */
export function AmountDisplay({
  minorUnits,
  currency,
  className,
  countUp = true,
  flashOnIncrease = false,
  resetKey,
}: {
  minorUnits: number
  currency: Currency
  className?: string
  countUp?: boolean
  flashOnIncrease?: boolean
  resetKey?: string
}) {
  const reduce = useReducedMotion()
  const skip = reduce || !countUp
  const mv = useMotionValue(skip ? minorUnits : 0)
  const [shown, setShown] = useState(skip ? minorUnits : 0)

  // Derived-state pattern: detect an increase during render, no effect needed.
  const [prev, setPrev] = useState({ value: minorUnits, key: resetKey })
  const [flashId, setFlashId] = useState(0)
  if (prev.value !== minorUnits || prev.key !== resetKey) {
    const increased = flashOnIncrease && prev.key === resetKey && minorUnits > prev.value
    setPrev({ value: minorUnits, key: resetKey })
    if (increased) setFlashId((n) => n + 1)
  }

  useEffect(() => {
    if (skip) {
      mv.set(minorUnits)
      return
    }
    const unsubscribe = mv.on('change', (v) => setShown(Math.round(v)))
    const controls = animate(mv, minorUnits, { duration: 0.7, ease: [0.22, 1, 0.36, 1] })
    return () => {
      unsubscribe()
      controls.stop()
    }
  }, [minorUnits, skip, mv])

  const value = skip ? minorUnits : shown
  const formatter = new Intl.NumberFormat('en-US', { style: 'currency', currency })
  const parts = formatter.formatToParts(value / 100)
  const full = formatter.format(minorUnits / 100)

  const digits = parts.map((part, i) =>
    part.type === 'decimal' || part.type === 'fraction' ? (
      <span key={i} className={styles.cents}>
        {part.value}
      </span>
    ) : (
      <span key={i}>{part.value}</span>
    ),
  )

  return (
    <motion.span
      key={flashOnIncrease ? flashId : 0}
      className={[styles.amount, 'amount', className].filter(Boolean).join(' ')}
      aria-label={full}
      initial={flashId > 0 && !reduce ? { scale: 1 } : false}
      animate={flashId > 0 && !reduce ? { scale: [1, 1.035, 1] } : undefined}
      transition={{ type: 'spring', stiffness: 380, damping: 14 }}
    >
      <span aria-hidden="true">{digits}</span>
      {flashId > 0 && (
        <motion.span
          key={flashId}
          aria-hidden="true"
          className={styles.flash}
          initial={{ opacity: 0 }}
          animate={{ opacity: [0, 1, 1, 0] }}
          transition={{ duration: 0.9, times: [0, 0.15, 0.55, 1], ease: 'easeOut' }}
        >
          {digits}
        </motion.span>
      )}
    </motion.span>
  )
}
