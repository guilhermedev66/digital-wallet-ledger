import { motion, useReducedMotion } from 'motion/react'

/** Hand-drawn SVG check (stroke draws in) over a mint glow burst. */
export function SuccessCheck({ size = 72 }: { size?: number }) {
  const reduce = useReducedMotion()
  return (
    <span className="relative inline-grid place-items-center" style={{ width: size, height: size }} aria-hidden="true">
      {!reduce && (
        <motion.span
          className="absolute inset-[-40%] rounded-full bg-[radial-gradient(circle,rgba(121,239,189,0.55)_0%,transparent_65%)]"
          initial={{ opacity: 0, scale: 0.4 }}
          animate={{ opacity: [0, 1, 0.25], scale: [0.4, 1.15, 1] }}
          transition={{ duration: 1.1, ease: 'easeOut' }}
        />
      )}
      <svg viewBox="0 0 72 72" width={size} height={size} fill="none" className="relative">
        <motion.circle
          cx="36" cy="36" r="31" stroke="var(--color-credit)" strokeWidth="4"
          initial={reduce ? false : { pathLength: 0, opacity: 0.4 }}
          animate={{ pathLength: 1, opacity: 1 }}
          transition={{ duration: 0.6, ease: 'easeOut' }}
        />
        <motion.path
          d="M22 37.5 31.5 47 50 26" stroke="var(--color-credit)" strokeWidth="5" strokeLinecap="round" strokeLinejoin="round"
          initial={reduce ? false : { pathLength: 0 }}
          animate={{ pathLength: 1 }}
          transition={{ duration: 0.45, delay: 0.5, ease: 'easeOut' }}
        />
      </svg>
    </span>
  )
}
