import { useEffect, useState } from 'react'
import { animate, useMotionValue, useReducedMotion } from 'motion/react'

/** Integer that counts up/down to its target (jumps under prefers-reduced-motion). */
export function CountUp({ value, className }: { value: number; className?: string }) {
  const reduce = useReducedMotion()
  const mv = useMotionValue(reduce ? value : 0)
  const [shown, setShown] = useState(reduce ? value : 0)

  useEffect(() => {
    if (reduce) {
      mv.set(value)
      return
    }
    const unsubscribe = mv.on('change', (v) => setShown(Math.round(v)))
    const controls = animate(mv, value, { duration: 0.8, ease: [0.22, 1, 0.36, 1] })
    return () => {
      unsubscribe()
      controls.stop()
    }
  }, [value, reduce, mv])

  const n = new Intl.NumberFormat('en-US')
  return (
    <span className={['amount', className].filter(Boolean).join(' ')} aria-label={n.format(value)}>
      <span aria-hidden="true">{n.format(reduce ? value : shown)}</span>
    </span>
  )
}
