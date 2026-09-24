import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { Kbd } from './Kbd'
import styles from './CommandPalette.module.css'

interface Command {
  id: string
  label: string
  hint: string
  run: () => void
}

export function CommandPalette({ isOpen, onClose }: { isOpen: boolean; onClose: () => void }) {
  const [query, setQuery] = useState('')
  const [activeIndex, setActiveIndex] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const navigate = useNavigate()
  const { logout } = useAuth()

  const commands = useMemo<Command[]>(
    () => [
      { id: 'dashboard', label: 'Go to dashboard', hint: 'wallets', run: () => navigate('/') },
      { id: 'transfer', label: 'Start a transfer', hint: 'move money', run: () => navigate('/transfer') },
      { id: 'activity', label: 'View activity', hint: 'transaction history', run: () => navigate('/activity') },
      { id: 'reconciliation', label: 'View reconciliation', hint: 'balance proof', run: () => navigate('/reconciliation') },
      { id: 'logout', label: 'Log out', hint: 'end session', run: () => logout() },
    ],
    [navigate, logout],
  )

  const filtered = useMemo(
    () => commands.filter((c) => c.label.toLowerCase().includes(query.toLowerCase())),
    [commands, query],
  )

  useEffect(() => {
    if (isOpen) {
      setQuery('')
      setActiveIndex(0)
      requestAnimationFrame(() => inputRef.current?.focus())
    }
  }, [isOpen])

  useEffect(() => {
    setActiveIndex(0)
  }, [query]);

  if (!isOpen) return null

  const runActive = () => {
    const command = filtered[activeIndex]
    if (command) {
      command.run()
      onClose()
    }
  }

  return (
    <div
      className={styles.overlay}
      onClick={onClose}
      role="presentation"
    >
      <div
        className={styles.panel}
        role="dialog"
        aria-modal="true"
        aria-label="Command palette"
        onClick={(e) => e.stopPropagation()}
      >
        <input
          ref={inputRef}
          className={styles.input}
          placeholder="Type a command…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Escape') onClose()
            else if (e.key === 'Tab') {
              // This input is the dialog's only focusable control - pin focus here
              // instead of letting Tab escape to background content behind the overlay.
              e.preventDefault()
            } else if (e.key === 'ArrowDown') {
              e.preventDefault()
              setActiveIndex((i) => Math.min(i + 1, filtered.length - 1))
            } else if (e.key === 'ArrowUp') {
              e.preventDefault()
              setActiveIndex((i) => Math.max(i - 1, 0))
            } else if (e.key === 'Enter') {
              e.preventDefault()
              runActive()
            }
          }}
          aria-activedescendant={filtered[activeIndex]?.id}
          role="combobox"
          aria-expanded="true"
          aria-controls="command-palette-list"
        />
        <ul id="command-palette-list" className={styles.list} role="listbox">
          {filtered.map((command, index) => (
            <li
              id={command.id}
              key={command.id}
              role="option"
              aria-selected={index === activeIndex}
              className={[styles.item, index === activeIndex ? styles.itemActive : ''].join(' ')}
              onMouseEnter={() => setActiveIndex(index)}
              onClick={() => {
                command.run()
                onClose()
              }}
            >
              <span>{command.label}</span>
              <span className="mono">{command.hint}</span>
            </li>
          ))}
          {filtered.length === 0 && <li className={styles.item}>No matching command</li>}
        </ul>
        <div className={styles.hint}>
          <span className={styles.hintItem}>
            <Kbd keys={['↑', '↓']} /> navigate
          </span>
          <span className={styles.hintItem}>
            <Kbd keys={['↵']} /> select
          </span>
          <span className={styles.hintItem}>
            <Kbd keys={['esc']} /> close
          </span>
        </div>
      </div>
    </div>
  )
}
