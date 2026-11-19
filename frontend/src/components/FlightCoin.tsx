import { animate, useReducedMotion } from 'motion/react'
import { useEffect, useRef, type RefObject } from 'react'

/**
 * A mint coin of light that flies in an arc from the source element to the destination
 * element (~600 ms, spring on x, arched y). Calls onArrive when it lands: "money moved".
 * Skipped entirely under prefers-reduced-motion (the parent just marks arrival).
 */
export function FlightCoin({
  containerRef,
  fromRef,
  toRef,
  onArrive,
}: {
  containerRef: RefObject<HTMLElement | null>
  fromRef: RefObject<HTMLElement | null>
  toRef: RefObject<HTMLElement | null>
  onArrive: () => void
}) {
  const reduce = useReducedMotion()
  const coin = useRef<HTMLSpanElement>(null)
  const arrive = useRef(onArrive)

  useEffect(() => {
    arrive.current = onArrive
  })

  useEffect(() => {
    const el = coin.current
    const box = containerRef.current?.getBoundingClientRect()
    const a = fromRef.current?.getBoundingClientRect()
    const b = toRef.current?.getBoundingClientRect()
    if (reduce || !el || !box || !a || !b) return
    const half = 16
    const x0 = a.left - box.left + a.width / 2 - half
    const y0 = a.top - box.top + a.height / 2 - half
    const x1 = b.left - box.left + b.width / 2 - half
    const y1 = b.top - box.top + b.height / 2 - half
    const lift = Math.max(70, Math.abs(x1 - x0) * 0.22)

    const fade = animate(el, { opacity: [0, 1, 1, 0.9] }, { duration: 0.6, times: [0, 0.12, 0.85, 1] })
    const arc = animate(el, { y: [y0, Math.min(y0, y1) - lift, y1], scale: [0.7, 1.15, 0.9] }, { duration: 0.6, times: [0, 0.5, 1], ease: ['easeOut', 'easeIn'] })
    const run = animate(el, { x: [x0, x1] }, { type: 'spring', duration: 0.6, bounce: 0.2 })
    let cancelled = false
    run.then(() => {
      if (cancelled) return
      animate(el, { opacity: 0, scale: 0.4 }, { duration: 0.18, ease: 'easeIn' })
      arrive.current()
    })
    return () => {
      cancelled = true
      fade.stop()
      arc.stop()
      run.stop()
    }
  }, [containerRef, fromRef, toRef, reduce])

  if (reduce) return null
  return (
    <span
      ref={coin}
      aria-hidden="true"
      className="pointer-events-none absolute left-0 top-0 z-20 size-8 rounded-full bg-[radial-gradient(circle_at_35%_30%,#ffffff_0%,#79efbd_45%,#2fbf8a_100%)] opacity-0 shadow-[0_0_22px_6px_rgba(121,239,189,0.65)]"
    />
  )
}
