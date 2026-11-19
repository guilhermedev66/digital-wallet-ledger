import { motion, useReducedMotion } from 'motion/react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import type { KeyboardEvent } from 'react'
import type { WalletSummary } from '../hooks/useWallets'
import { TiltCard } from './tilt-card'
import { WalletCreditCard } from './WalletCreditCard'

/** Stacked card carousel: the active card sits in front with 3D tilt, the others peek behind. */
export function WalletStack({
  wallets,
  activeId,
  onChange,
}: {
  wallets: WalletSummary[]
  activeId: string
  onChange: (id: string) => void
}) {
  const reduce = useReducedMotion()
  const n = wallets.length
  const activeIndex = Math.max(0, wallets.findIndex((w) => w.id === activeId))

  const go = (delta: number) => onChange(wallets[(activeIndex + delta + n) % n].id)

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (n < 2) return
    if (event.key === 'ArrowRight') {
      event.preventDefault()
      go(1)
    } else if (event.key === 'ArrowLeft') {
      event.preventDefault()
      go(-1)
    }
  }

  return (
    <div
      role="group"
      aria-roledescription="carousel"
      aria-label="Your wallets"
      onKeyDown={onKeyDown}
      className="mx-auto flex w-full max-w-[460px] flex-col items-center gap-5"
    >
      <div className="relative mt-9 aspect-[1.586/1] w-full">
        {wallets.map((wallet, i) => {
          const offset = (i - activeIndex + n) % n // 0 = front
          const hidden = offset > 2
          return (
            <motion.div
              key={wallet.id}
              className="absolute inset-0"
              initial={false}
              animate={{
                y: -offset * 24,
                scale: 1 - offset * 0.07,
                opacity: hidden ? 0 : 1 - offset * 0.28,
                filter: `brightness(${1 - offset * 0.18})`,
              }}
              transition={reduce ? { duration: 0 } : { type: 'spring', stiffness: 220, damping: 26 }}
              style={{ zIndex: 20 - offset, pointerEvents: hidden ? 'none' : 'auto' }}
            >
              {offset === 0 ? (
                <TiltCard
                  tiltLimit={reduce ? 0 : 9}
                  scale={reduce ? 1 : 1.02}
                  effect="gravitate"
                  perspective={1100}
                  className="size-full rounded-[28px]"
                >
                  <WalletCreditCard wallet={wallet} />
                </TiltCard>
              ) : (
                <button
                  type="button"
                  onClick={() => onChange(wallet.id)}
                  aria-label={`Show wallet ${wallet.displayName ?? 'Untitled wallet'} (${wallet.currency})`}
                  className="block size-full cursor-pointer rounded-[28px] border-0 bg-transparent p-0"
                >
                  <WalletCreditCard wallet={wallet} />
                </button>
              )}
            </motion.div>
          )
        })}
      </div>

      {n > 1 && (
        <div className="flex items-center gap-4">
          <button
            type="button"
            onClick={() => go(-1)}
            aria-label="Previous wallet"
            className="grid size-9 cursor-pointer place-items-center rounded-full border border-[var(--color-border-strong)] bg-transparent text-[var(--color-text)] transition hover:bg-[var(--color-surface-elevated)] active:scale-90"
          >
            <ChevronLeft size={18} aria-hidden="true" />
          </button>
          <div className="flex items-center gap-2" role="tablist" aria-label="Choose wallet">
            {wallets.map((w) => (
              <button
                key={w.id}
                type="button"
                role="tab"
                aria-selected={w.id === activeId}
                aria-label={`${w.displayName ?? 'Untitled wallet'} (${w.currency})`}
                onClick={() => onChange(w.id)}
                className={[
                  'h-2.5 cursor-pointer rounded-full border-0 p-0 transition-all duration-300',
                  w.id === activeId ? 'w-7 bg-[var(--color-accent)]' : 'w-2.5 bg-[var(--color-border-strong)]',
                ].join(' ')}
              />
            ))}
          </div>
          <button
            type="button"
            onClick={() => go(1)}
            aria-label="Next wallet"
            className="grid size-9 cursor-pointer place-items-center rounded-full border border-[var(--color-border-strong)] bg-transparent text-[var(--color-text)] transition hover:bg-[var(--color-surface-elevated)] active:scale-90"
          >
            <ChevronRight size={18} aria-hidden="true" />
          </button>
        </div>
      )}
    </div>
  )
}
